using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.DoubleNumerics;
using KdTree;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;

namespace partmake
{
    namespace Topology
    {
        public class ConnectorUtils
        { 
            static public List<Tuple<Vector3, Plane>> GetRStuds(Mesh m, Vector3[] candidates, List<Tuple<Vector3, Vector3>> bisectors)
            {
                AABB aabb = AABB.CreateFromPoints(m.vertices.Select(v => v.pt).ToList());
                Dictionary<int, Plane> planes = new Dictionary<int, Plane>();
                Plane bottomPlane = m.planeMgr.GetPlane(Vector3.UnitY, aabb.Max.Y);
                foreach (Face f in m.faces)
                {
                    if (f.Plane.totalArea > 100.0 || f.Plane.idx == bottomPlane.idx)
                    {
                        if (!planes.ContainsKey(f.Plane.idx))
                            planes.Add(f.Plane.idx, f.Plane);
                    }
                }
                List<Tuple<Vector3, Plane>> outPts = new List<Tuple<Vector3, Plane>>();
                foreach (var kvplane in planes)
                {
                    List<Face> planeFaces =
                        m.faces.Where(f => f.Plane.idx == kvplane.Key).ToList();
                    foreach (var kv in m.edgeDict)
                    {
                        kv.Value.flag = 0;
                    }
                    List<Edge> edges = new List<Edge>();
                    List<Vector3> candidatePts = new List<Vector3>();
                    {
                        KdTree<double, Vector3> kd = new KdTree<double, Vector3>(3, new KdTree.Math.DoubleMath());
                        m.GetPlaneEdges(kvplane.Value, edges);

                        if (edges.Count == 0)
                            continue;

                        Vector3 planeNormal = kvplane.Value.normal;
                        double planeDist = Vector3.Dot(edges[0].v0.pt, planeNormal);
                        foreach (Vector3 c in candidates)
                        {
                            Vector3 pt = c - planeNormal * Vector3.Dot(c, planeNormal);
                            pt += planeNormal * planeDist;
                            if (Mesh.IsUnique(kd, pt))
                                candidatePts.Add(pt);
                        }

                        foreach (var edge in edges)
                        {
                            edge.flag = 1;
                        }
                        foreach (var edge in edges)
                        {
                            if (edge.len < 6.0f)
                                continue;
                            foreach (Edge e in edge.v0.edges)
                            {
                                if (e == edge || e.flag != 1 || e.len < 6.0f)
                                    continue;
                                Vector3 v0 = edge.v0.pt;
                                Vector3 v1a = edge.v1.pt;
                                Vector3 v1adir = Vector3.Normalize(v1a - v0);
                                Vector3 v1b = (edge.v0.idx == e.v0.idx) ? e.v1.pt : e.v0.pt;
                                Vector3 v1bdir = Vector3.Normalize(v1b - v0);

                                Vector3 bisectAngle = Vector3.Normalize(v1adir + v1bdir);
                                double sinTheta = Vector3.Cross(bisectAngle, v1adir).Length();
                                Vector3 testPt = v0 + (6.0f / sinTheta) * bisectAngle;
                                bisectors.Add(new Tuple<Vector3, Vector3>(v0, testPt));
                                if (Mesh.IsUnique(kd, testPt))
                                    candidatePts.Add(testPt);
                            }

                            foreach (Edge e in edge.v1.edges)
                            {
                                if (e == edge || e.flag != 1 || e.len < 6.0f)
                                    continue;

                                Vector3 v0 = edge.v1.pt;
                                Vector3 v1a = edge.v0.pt;
                                Vector3 v1adir = Vector3.Normalize(v1a - v0);
                                Vector3 v1b = (edge.v1.idx == e.v0.idx) ? e.v1.pt : e.v0.pt;
                                Vector3 v1bdir = Vector3.Normalize(v1b - v0);

                                Vector3 bisectAngle = Vector3.Normalize(v1adir + v1bdir);
                                double sinTheta = Vector3.Cross(bisectAngle, v1adir).Length();
                                Vector3 testPt = v0 + (6.0f / sinTheta) * bisectAngle;
                                bisectors.Add(new Tuple<Vector3, Vector3>(v0, testPt));
                                if (Mesh.IsUnique(kd, testPt))
                                    candidatePts.Add(testPt);
                            }

                            if (Eps.Eq(edge.len, 12))
                            {
                                Vector3 edgeNrm = Vector3.Cross(planeNormal, edge.dir);
                                Vector3 pt1 = (edge.v0.pt + edge.v1.pt) * 0.5f +
                                    edgeNrm * 6.0f;
                                if (Mesh.IsUnique(kd, pt1))
                                    candidatePts.Add(pt1);
                                Vector3 pt2 = (edge.v0.pt + edge.v1.pt) * 0.5f -
                                    edgeNrm * 6.0f;
                                if (Mesh.IsUnique(kd, pt2))
                                    candidatePts.Add(pt2);
                            }
                        }
                    }

                    foreach (Vector3 cpt in candidatePts)
                    {
                        List<Tuple<Edge, Vector3>> touchedEdges = new List<Tuple<Edge, Vector3>>();
                        bool hasBlockage = false;
                        foreach (var e in edges)
                        {
                            Vector3 nearestPt = Mesh.NearestPt(e.v0.pt, e.v1.pt, cpt);

                            Vector3 nrmVec = (nearestPt - cpt);
                            double lenSq = (nrmVec).LengthSquared();
                            if (lenSq > 34 && lenSq < 38)
                            {
                                touchedEdges.Add(new Tuple<Edge, Vector3>(e, Vector3.Normalize(nrmVec)));
                            }
                            else if (lenSq < 34)
                                hasBlockage = true;
                        }

                        if (hasBlockage)
                            continue;
                        if (touchedEdges.Count() >= 3)
                        {
                            bool isOnFace =
                                planeFaces.Any(f => f.aabb.Contains(cpt) != AABB.ContainmentType.Disjoint
                                && f.IsPointOnPoly(cpt));
                            if (!isOnFace)
                            {
                                double minDot = 1;
                                for (int i = 0; i < touchedEdges.Count; i++)
                                {
                                    for (int j = i + 1; j < touchedEdges.Count; j++)
                                    {
                                        double dot = Vector3.Dot(touchedEdges[i].Item2, touchedEdges[j].Item2);
                                        minDot = Math.Min(minDot, dot);
                                    }
                                }
                                if (minDot < 0)
                                    outPts.Add(new Tuple<Vector3, Plane>(cpt, kvplane.Value));
                            }
                        }
                    }
                }
                outPts = outPts.Distinct().ToList();
                return outPts;
            }

        }
    }
}
