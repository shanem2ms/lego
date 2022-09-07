﻿using System;
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

        public BulletSimulation.DebugDrawLineDel DebugDrawLine
        {
            get => bulletSimulation.DebugDrawLine;
            set => bulletSimulation.DebugDrawLine = value;
        }

        public void DrawBulletDebug()
        {
            bulletSimulation.DrawDebug();
        }

        public void Rebuild(List<PartInst> newPartList, List<Vector4> newLocators,
            PartInst playerPart)
        {
            this.PartList.Clear();
            this.PartList.AddRange(newPartList);
            this.PlayerPart = playerPart;
            var dynamicConnections = ProcessConnections();
            this.DebugLocators.Clear();
            this.DebugLocators.AddRange(newLocators);
            this.bulletSimulation.Clear();
            this.PartList.Sort((p1, p2) => p1.grpId.CompareTo(p2.grpId));
            var groups = PartList.GroupBy(p => p.grpId);
            foreach (var partGroup in groups)
            {
                RigidBody rb = new RigidBody(partGroup);
                this.bulletSimulation.AddObj(rb);
            }

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

        bool fwdPressed = false;
        bool backPressed = false;
        bool leftPressed = false;
        bool rightPressed = false;
        public void GameLoop()
        {
            if (PlayerPart != null)
            {
                if (fwdPressed)
                {
                    this.PlayerPart.body.Body.Friction = 0;
                    this.PlayerPart.body.Body.ApplyCentralImpulse(new BulletSharp.Math.Vector3(2000, 2000, 0));
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
