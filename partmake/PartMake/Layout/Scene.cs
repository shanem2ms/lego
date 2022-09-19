using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using BulletSharp;
using Amazon.S3.Model;
using BulletSharp.SoftBody;
using partmake.Topology;
using static partmake.Topology.Convex;
using System.Threading;
using MathNet.Numerics.Distributions;
using partmake.script;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace partmake
{
    public class Scene
    {
        BulletSimulation bulletSimulation = new BulletSimulation();

        public Scene()
        {
            bulletSimulation.DebugDraw = DebugDrawModes.DrawConstraints;
        }
        public List<PartInst> PartList { get; set; } = new List<PartInst>();
        public List<Vector4> DebugLocators { get; set; } = new List<Vector4>();

        public PartInst PlayerPart { get; set; } = null;
        OctTree octTree = new OctTree();

        public void BeginUpdate(bool clearScene)
        {
            Monitor.Enter(this.PartList);
            if (clearScene)
            {
                octTree = new OctTree();
                this.PartList.Clear();
                this.DebugLocators.Clear();
            }
        }

        public void EndUpdate()
        {
            Monitor.Exit(this.PartList);
            Rebuild();
        }
        public BulletSimulation.DebugDrawLineDel DebugDrawLine
        {
            get => bulletSimulation.DebugDrawLine;
            set => bulletSimulation.DebugDrawLine = value;
        }

        public void DrawBulletDebug()
        {
            bulletSimulation.DrawDebug();
        }

        public void Rebuild()
        {
            var dynamicConnections = ProcessConnections();
            this.bulletSimulation.Clear();
            this.PartList.Sort((p1, p2) => p1.grpId.CompareTo(p2.grpId));
            var groups = PartList.GroupBy(p => p.grpId);
            foreach (var partGroup in groups)
            {
                if (partGroup.First().grpId == -1)
                    continue;
                RigidBody rb = new RigidBody(partGroup);
                this.bulletSimulation.AddObj(rb);
            }

            this.octTree.AddColliders(this.bulletSimulation);

            foreach (var connection in dynamicConnections)
            {
                var c0Pos = Vector3.Transform(
                    connection.Connector0.Pos, connection.p0.bodySubMat);
                var c0Dir = Vector3.TransformNormal(
                    connection.Connector0.DirY, connection.p0.bodySubMat);
                var c1Pos = Vector3.Transform(
                    connection.Connector1.Pos, connection.p1.bodySubMat);
                var c1Dir = Vector3.TransformNormal(
                    connection.Connector1.DirY, connection.p1.bodySubMat);
                Hinge pc = new Hinge(connection.p0.body,
                    c0Pos, c0Dir, connection.p1.body, c1Pos, c1Dir);
                this.bulletSimulation.AddConst(pc);
            }
        }

        HashSet<ConnectionInst> ProcessConnections()
        {
            foreach (var part in PartList)
            {
                part.grpId = -1;
            }

            int curGroup = 1;

            var anchoredGroups = PartList.GroupBy(p => p.anchored);
            List<ConnectionInst> connections = new List<ConnectionInst>();
            foreach (var anchoredGroup in anchoredGroups)
            {
                bool anchored = anchoredGroup.First().anchored;
                if (!anchored)
                {
                    while (true)
                    {
                        var nextPart = anchoredGroup.FirstOrDefault((p) => p.grpId == -1);
                        if (nextPart == null)
                            break;
                        MarkPartGroup(nextPart, anchored ? 0 : curGroup, connections);
                        if (!anchored)
                            curGroup++;
                    }
                }
            }

            return connections.ToHashSet();
        }

        void MarkPartGroup(PartInst p, int grpId, List<ConnectionInst> connections)
        {
            if (p.grpId != -1)
                return;
            p.grpId = grpId;
            foreach (ConnectionInst c in p.connections)
            {
                if (c != null)
                {
                    var connectInfo = c.ConnectInfo;
                    if (connectInfo.dynamicJoint == ConnectionInfo.DynamicJoint.Weld)
                    {
                        PartInst other = c.p1 == p ? c.p0 : c.p1;
                        MarkPartGroup(other, grpId, connections);
                    }
                    else
                        connections.Add(c);
                }
            }
        }
        public void AddUnconnected(PartInst pi)
        {
            this.PartList.Add(pi);
            this.octTree.AddPart(pi);
        }
        public bool Connect(PartInst pi1, int connectorIdx1, PartInst pi0,
            int connectorIdx0, bool allowCollisions)
        {
            var ci0 = pi0.item.Connectors[connectorIdx0];
            var ci1 = pi1.item.Connectors[connectorIdx1];

            System.Numerics.Matrix4x4 m2 =
                ci1.IM44 * ci0.M44 * pi0.mat;
            pi1.mat = m2;
            ConnectionInst cinst = new ConnectionInst()
            {
                p0 = pi0,
                c0 = connectorIdx0,
                p1 = pi1,
                c1 = connectorIdx1
            };
            pi0.connections[connectorIdx0] = cinst;
            pi1.connections[connectorIdx1] = cinst;
            if (!allowCollisions && octTree.CollisionCheck(pi1))
                return false;
            this.PartList.Add(pi1);
            this.octTree.AddPart(pi1);
            return true;
        }

        public void SetPlayerPart(PartInst piPlayer)
        {
            this.PlayerPart = piPlayer;
        }

        bool fwdPressed = false;
        bool backPressed = false;
        bool leftPressed = false;
        bool rightPressed = false;
        public void GameLoop(Vector2 lookDir, Vector3 cameraPos)
        {
            Matrix4x4 viewDir = Matrix4x4.CreateRotationY(lookDir.X) *
                    Matrix4x4.CreateRotationX(lookDir.Y);
            Vector3 zDirPrime = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, viewDir));
            Vector3 xDir = Vector3.Cross(zDirPrime, Vector3.UnitY);
            Vector3 zDir = Vector3.Cross(Vector3.UnitY, xDir);
            Vector3 impulseVel = Vector3.Zero;
            if (fwdPressed) impulseVel += xDir;
            if (backPressed) impulseVel -= xDir;
            if (leftPressed) impulseVel -= zDir;
            if (rightPressed) impulseVel += zDir;
            impulseVel *= 2000;
            if (PlayerPart != null)
            {
                if (impulseVel.LengthSquared() > 10)
                {
                    this.PlayerPart.body.Body.Friction = 0;
                    this.PlayerPart.body.Body.ApplyCentralImpulse(new BulletSharp.Math.Vector3(impulseVel.X, impulseVel.Y, impulseVel.Z));
                    this.PlayerPart.body.Body.Activate();
                }
            }
            bulletSimulation.Step();            
        }

        public void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case System.Windows.Input.Key.W:
                    fwdPressed = true;
                    break;
                case System.Windows.Input.Key.S:
                    backPressed = true;
                    break;
                case System.Windows.Input.Key.A:
                    leftPressed = true;
                    break;
                case System.Windows.Input.Key.D:
                    rightPressed = true;
                    break;
            }
        }

        public void OnKeyUp(System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case System.Windows.Input.Key.W:
                    fwdPressed = false;
                    break;
                case System.Windows.Input.Key.S:
                    backPressed = false;
                    break;
                case System.Windows.Input.Key.A:
                    leftPressed = false;
                    break;
                case System.Windows.Input.Key.D:
                    rightPressed = false;
                    break;
            }
        }

    }
}
