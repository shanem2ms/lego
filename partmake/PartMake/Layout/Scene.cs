using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using BulletSharp;

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

        public void Rebuild(List<PartInst> newPartList, List<Vector4> newLocators)
        {
            PartList.Clear();
            PartList.AddRange(newPartList);
            ProcessConnections();
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
        }

        void ProcessConnections()
        {
            foreach (var part in PartList)
            {
                part.grpId = -1;
            }

            int curGroup = 0;

            while (true)
            {
                var nextPart = PartList.FirstOrDefault((p) => p.grpId == -1);
                if (nextPart == null)
                    break;
                MarkPartGroup(nextPart, curGroup);
                curGroup++;
            }
        }

        void MarkPartGroup(PartInst p, int grpId)
        {
            if (p.grpId != -1)
                return;
            p.grpId = grpId;
            foreach (ConnectionInst c in p.connections)
            {
                if (c != null)
                {
                    PartInst other = c.p1 == p ? c.p0 : c.p1;
                    MarkPartGroup(other, grpId);
                }
            }
        }
    }
}
