using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using BulletSharp;
using Amazon.S3.Model;

namespace partmake
{
    public class Scene
    {
        BulletSimulation bulletSimulation = new BulletSimulation();

        public Scene()
        {
            bulletSimulation.DebugDraw = DebugDrawModes.DrawWireframe;
        }
        public List<PartInst> PartList { get; set; } = new List<PartInst>();
        public List<Vector4> DebugLocators { get; set; } = new List<Vector4>();

        public BulletSimulation.DebugDrawLineDel DebugDrawLine { get => bulletSimulation.DebugDrawLine;
            set => bulletSimulation.DebugDrawLine = value; }

        public void StepSimulation()
        {
            bulletSimulation.Step();
        }

        public void DrawBulletDebug()
        {
            bulletSimulation.DrawDebug();
        }

        public void Rebuild(List<PartInst> newPartList, List<Vector4> newLocators)
        {
            PartList.Clear();
            PartList.AddRange(newPartList);
            var dynamicConnections = ProcessConnections();
            DebugLocators.Clear();
            DebugLocators.AddRange(newLocators);
            bulletSimulation.Clear();
            PartList.Sort((p1, p2) => p1.grpId.CompareTo(p2.grpId));
            var groups = PartList.GroupBy(p => p.grpId);
            foreach (var partGroup in groups)
            {
                RigidBody rb = new RigidBody(partGroup);
                bulletSimulation.AddObj(rb);
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
                bulletSimulation.AddConst(pc);
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
    }
}
