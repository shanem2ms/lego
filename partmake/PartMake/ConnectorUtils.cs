using System;
using System.Collections.Generic;
using System.Linq;
using System.DoubleNumerics;
using KdTree;

namespace partmake.Topology
{
    public class ConnectorUtils
    {
        static public Dictionary<int, Plane> GetCandidatePlanes(Topology.Mesh m)
        {
            Dictionary<int, Plane> planes = new Dictionary<int, Plane>();
            Plane bottomPlane = m.planeMgr.GetPlane(Vector3.UnitY, 0);
            foreach (Face f in m.faces)
            {
                if (f.Plane.totalArea > 25 || f.Plane.idx == bottomPlane.idx)
                {
                    if (!planes.ContainsKey(f.Plane.idx))
                        planes.Add(f.Plane.idx, f.Plane);
                }
            }

            Dictionary<int, Plane> planes2 = new Dictionary<int, Plane>();
            foreach (var kv in planes)
            {
                Plane p = kv.Value;
                bool[] pn = { false, false };
                foreach (var v in m.vertices)
                {
                    double dist = Vector3.Dot(v.pt, p.normal) - p.d;
                    if (!Eps.Eq2(dist, 0))
                    {
                        if (dist < 0) pn[0] = true;
                        else pn[1] = true;
                    }
                }
                if (pn[0] ^ pn[1])
                    planes2.Add(kv.Key, kv.Value);
            }
            return planes2;
        }
        static public List<Tuple<Vector3, Topology.Plane>> GetRStuds(Topology.Mesh m, Vector3[] candidates, List<Tuple<Vector3, Vector3>> bisectors)
        {
            Dictionary<int, Plane> planes = GetCandidatePlanes(m);
            AABB aabb = AABB.CreateFromPoints(m.vertices.Select(v => v.pt).ToList());
            List<Tuple<Vector3, Plane>> outPts = new List<Tuple<Vector3, Plane>>();
            foreach (var kvplane in planes)
            {
                List<Face> planeFaces =
                    m.faces.Where(f => f.Plane.idx == kvplane.Key).ToList();
                foreach (var kv in m.edgeDict)
                {
                    kv.Value.flag = 0;
                }
                HashSet<Edge> edges = new HashSet<Edge>();
                List<Vector3> candidatePts = new List<Vector3>();
                {
                    KdTree<double, Vector3> kd = new KdTree<double, Vector3>(3, new KdTree.Math.DoubleMath());
                    m.GetPlaneEdges(kvplane.Value, edges);

                    if (edges.Count == 0)
                        continue;

                    Vector3 planeNormal = kvplane.Value.normal;
                    double planeDist = Vector3.Dot(edges.First().v0.pt, planeNormal);
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

        static public List<Tuple<Vector3, Topology.Plane>> GetCandidates(Topology.Mesh m, Plane p,
            List<Tuple<Vector3, Vector3>> bisectors, float minsize,
            float minradius, float maxradius)
        {
            AABB aabb = AABB.CreateFromPoints(m.vertices.Select(v => v.pt).ToList());
            List<Tuple<Vector3, Plane>> outPts = new List<Tuple<Vector3, Plane>>();

            List<Face> planeFaces =
                m.faces.Where(f => f.Plane.idx == p.idx).ToList();
            foreach (var kv in m.edgeDict)
            {
                kv.Value.flag = 0;
            }
            HashSet<Edge> edges = new HashSet<Edge>();
            List<Vector3> candidatePts = new List<Vector3>();
            {
                KdTree<double, Vector3> kd = new KdTree<double, Vector3>(3, new KdTree.Math.DoubleMath());
                m.GetPlaneEdges(p, edges);

                if (edges.Count == 0)
                    return null;

                Vector3 planeNormal = p.normal;

                foreach (var edge in edges)
                {
                    edge.flag = 1;
                }
                foreach (var edge in edges)
                {
                    if (edge.len < minsize)
                        continue;
                    foreach (Edge e in edge.v0.edges)
                    {
                        if (e == edge || e.flag != 1 || e.len < minsize)
                            continue;
                        if (!edges.Contains(e))
                            continue;
                        Vector3 v0 = edge.v0.pt;
                        Vector3 v1a = edge.v1.pt;
                        Vector3 v1adir = Vector3.Normalize(v1a - v0);
                        Vector3 v1b = (edge.v0.idx == e.v0.idx) ? e.v1.pt : e.v0.pt;
                        Vector3 v1bdir = Vector3.Normalize(v1b - v0);

                        Vector3 bisectAngle = Vector3.Normalize(v1adir + v1bdir);
                        double sinTheta = Vector3.Cross(bisectAngle, v1adir).Length();
                        Vector3 testPt = v0 + (minsize / sinTheta) * bisectAngle;
                        bisectors.Add(new Tuple<Vector3, Vector3>(v0, testPt));
                        if (Mesh.IsUnique(kd, testPt))
                            candidatePts.Add(testPt);
                    }

                    foreach (Edge e in edge.v1.edges)
                    {
                        if (e == edge || e.flag != 1 || e.len < minsize)
                            continue;
                        if (!edges.Contains(e))
                            continue;

                        Vector3 v0 = edge.v1.pt;
                        Vector3 v1a = edge.v0.pt;
                        Vector3 v1adir = Vector3.Normalize(v1a - v0);
                        Vector3 v1b = (edge.v1.idx == e.v0.idx) ? e.v1.pt : e.v0.pt;
                        Vector3 v1bdir = Vector3.Normalize(v1b - v0);

                        Vector3 bisectAngle = Vector3.Normalize(v1adir + v1bdir);
                        double sinTheta = Vector3.Cross(bisectAngle, v1adir).Length();
                        Vector3 testPt = v0 + (minsize / sinTheta) * bisectAngle;
                        bisectors.Add(new Tuple<Vector3, Vector3>(v0, testPt));
                        if (Mesh.IsUnique(kd, testPt))
                            candidatePts.Add(testPt);
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
                    if (lenSq > minradius && lenSq < maxradius)
                    {
                        touchedEdges.Add(new Tuple<Edge, Vector3>(e, Vector3.Normalize(nrmVec)));
                    }
                    else if (lenSq < minradius)
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
                            outPts.Add(new Tuple<Vector3, Plane>(cpt, p));
                    }
                }
            }
            outPts = outPts.Distinct().ToList();
            return outPts;
        }


        static public void FindCirlces(Mesh m, Plane p)
        {
            KdTree<double, Vector3> kd = new KdTree<double, Vector3>(3, new KdTree.Math.DoubleMath());
            HashSet<Edge> planeEdges = new HashSet<Edge>();
            m.GetPlaneEdges(p, planeEdges);
            foreach (Edge e in planeEdges)
            {
                e.flag = 0;
            }
            foreach (Edge e in planeEdges)
            {
                FollowEdge(e, planeEdges);
            }

        }

        static void FollowEdge(Edge e, HashSet<Edge> planeEdges)
        {
            e.flag = 1;
            foreach (Edge e1 in e.v1.edges)
            {
                if (e.flag == 1)
                    continue;
                Vector3.Dot(e.dir, e1.dir);
            }
            e.flag = 0;
        }
        static public List<Loop> FindLoops(Mesh m)
        {
            List<Loop> foundLoops = new List<Loop>();
            foreach (Face f in m.faces)
            {
                Vector3 nrm = f.Normal;
                foreach (EdgePtr eptr in f.edges)
                {
                    Edge e = eptr.e;
                    if (e.flag == 0)
                    {
                        List<Edge> edges = new List<Edge>() { e };
                        if (FollowCoplanarEdges(edges, nrm))
                        {
                            foreach (Edge fe in edges)
                            {
                                fe.flag = 1;
                            }
                            foundLoops.Add(new Loop() { edges = edges });
                        }
                    }
                }
            }

            return foundLoops;
        }

        static bool FollowCoplanarEdges(List<Edge> edges, Vector3 nrm)
        {
            Edge cur = edges.Last();
            if (cur.instack)
            {
                if (edges[0] == cur && edges.Count > 3)
                    return true;
                else
                    return false;
            }
            cur.instack = true;
            foreach (Edge e in cur.v1.edges)
            {
                if (e.flag == 0 &&
                    Vector3.Dot(nrm, e.dir) == 0)
                {
                    edges.Add(e);
                    if (FollowCoplanarEdges(edges, nrm))
                    {
                        cur.instack = false;
                        return true;
                    }
                    edges.Remove(e);
                }
            }

            cur.instack = false;
            return false;
        }

    }
}
