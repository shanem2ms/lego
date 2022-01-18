using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Numerics;
using KdTree;

namespace partmake
{
    namespace Topology
    {
        public class Vertex
        {
            public int idx;
            public Vector3 pt;
            public List<Edge> edges =
                new List<Edge>();
        }

        public class Loop
        {
            public List<Edge> edges;

            public void GetVertices(List<Vtx> vertices)
            {
                Vector3 anchor = edges[0].v0.pt;
                Vector3 nrm = Vector3.Normalize(Vector3.Cross(edges[0].dir, edges[1].dir));
                for (int i = 1; i < edges.Count - 1; ++i)
                {
                    vertices.Add(new Vtx(anchor, nrm, new Vector2(0, 0)));
                    vertices.Add(new Vtx(edges[i].v0.pt, nrm, new Vector2(0, 0)));
                    vertices.Add(new Vtx(edges[i].v1.pt, nrm, new Vector2(0, 0)));
                }
            }
        }

        public class Edge
        {
            public Edge(Vertex _v0, Vertex _v1)
            {
                if (_v0.idx < _v1.idx)
                { v0 = _v0; v1 = _v1; }
                else
                { v1 = _v0; v0 = _v1; }
                dir = Vector3.Normalize(v1.pt - v0.pt);
                len = (v1.pt - v0.pt).Length();
                aabb = AABB.CreateFromPoints(new Vector3[] { v0.pt, v1.pt });
            }
            public Vertex v0;
            public Vertex v1;
            public Vector3 dir;
            public float len;
            public int flag; // for processing
            public bool instack = false;
            public List<EdgePtr> edgePtrs =
                new List<EdgePtr>();
            public AABB aabb;


            public ulong ComputeHash()
            {
                UInt64 hashedValue = 3074457345618258791ul;
                hashedValue += (ulong)v0.idx;
                hashedValue *= 3074457345618258799ul;
                hashedValue += (ulong)v1.idx;
                hashedValue *= 3074457345618258799ul;
                return hashedValue;
            }

        }

        public class EdgePtr
        {
            public Edge e;
            public bool reverse;
            public Face parentFace;
            public Vector3 Dir => reverse ? -e.dir : e.dir;
            public Vertex V0 => reverse ? e.v1 : e.v0;
            public Vertex V1 => reverse ? e.v0 : e.v1;
        }
        public class Face
        {
            public List<EdgePtr> edges;
            public List<Vertex> vtx;
            public bool visited;
            public bool isinvalid = false;

            public void GetVertices(List<Vertex> v)
            {
                //v.AddRange(vtx);

                HashSet<int> vidxSet = new HashSet<int>();

                foreach (EdgePtr e in edges)
                {
                    if (!vidxSet.Contains(e.V0.idx))
                    {
                        v.Add(e.V0);
                        vidxSet.Add(e.V0.idx);
                    }
                    if (!vidxSet.Contains(e.V1.idx))
                    {
                        v.Add(e.V1);
                        vidxSet.Add(e.V1.idx);
                    }
                }
            }

            public ulong ComputeHash()
            {
                List<Vertex> vlist = new List<Vertex>();
                GetVertices(vlist);
                vlist.Sort((a, b) => a.idx.CompareTo(b.idx));
                UInt64 hashedValue = 3074457345618258791ul;
                foreach (Vertex v in vlist)
                {
                    hashedValue += (ulong)v.idx;
                    hashedValue *= 3074457345618258799ul;
                }
                return hashedValue;
            }

            public Vector3 Normal
            {
                get
                {
                    return Vector3.Normalize(Vector3.Cross(edges[0].Dir, edges[1].Dir));
                }
            }
        }


        public class Mesh
        {
            public KdTree<float, Vertex> kdTree = new KdTree<float, Vertex>(3, new KdTree.Math.FloatMath());
            List<Vertex> vertices = new List<Vertex>();
            public List<Face> faces = new List<Face>();
            int nextVtxIdx = 0;
            public Dictionary<ulong, Edge> edgeDict = new Dictionary<ulong, Edge>();

            public static bool IsEqual(float[] vals, Vector3 v2)
            {
                Vector3 v1 = new Vector3(vals[0], vals[1], vals[2]);
                return Vector3.DistanceSquared(v1, v2) < 0.00001f;
            }
            public void AddFace(List<Vector3> vertices)
            {
                List<Vertex> verlist = new List<Vertex>();
                foreach (var v in vertices)
                {
                    var nodes = kdTree.GetNearestNeighbours(new float[] { v.X, v.Y, v.Z }, 1);
                    if (nodes.Length > 0 && IsEqual(nodes[0].Point, v))
                    {
                        verlist.Add(nodes[0].Value);
                    }
                    else
                    {
                        Vertex nv = new Vertex() { pt = v, idx = nextVtxIdx++ };
                        verlist.Add(nv);
                        kdTree.Add(new float[] { v.X, v.Y, v.Z }, nv);
                    }
                }
                List<EdgePtr> elist = new List<EdgePtr>();
                for (int idx = 0; idx < verlist.Count; ++idx)
                {
                    Vertex v0 = verlist[idx];
                    Vertex v1 = verlist[(idx + 1) % verlist.Count];
                    bool isreverse = v1.idx < v0.idx;
                    Edge e =
                        new Edge(v0, v1);
                    ulong hash = e.ComputeHash();
                    if (edgeDict.ContainsKey(hash))
                        e = edgeDict[hash];
                    else
                        edgeDict.Add(hash, e);

                    e.v0.edges.Add(e);
                    e.v1.edges.Add(e);
                    EdgePtr eptr = new EdgePtr() { e = e, reverse = isreverse };
                    e.edgePtrs.Add(eptr);
                    elist.Add(eptr);
                }

                Face f = new Face() { edges = elist, vtx = verlist };
                foreach (EdgePtr eptr in f.edges)
                {
                    eptr.parentFace = f;
                }
                this.vertices.AddRange(verlist);
                this.faces.Add(f);
            }


            bool FollowEdge(List<Edge> edges)
            {
                Edge cur = edges.Last();
                if (cur.instack)
                {
                    if (edges[0] == cur && edges.Count() == 5)
                        return true;
                    else
                        return false;
                }
                cur.instack = true;
                foreach (Edge e in cur.v1.edges)
                {
                    if (e.flag == 0 &&
                        e.len == 12 && Vector3.Dot(cur.dir, e.dir) == 0)
                    {
                        edges.Add(e);
                        if (FollowEdge(edges))
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

            public void Fix()
            {
                //SplitEdges();
                RemoveDuplicateFaces();
                FixWindings();
            }

            void SplitEdges()
            {
                foreach (var kv in edgeDict)
                {
                    Edge e = kv.Value;
                    AABB aabb = AABB.CreateFromPoints(new List<Vector3>()
                    { e.v0.pt, e.v1.pt });
                    float elen = (e.v1.pt - e.v0.pt).Length();

                    foreach (var vtx in vertices)
                    {
                        if (aabb.Contains(vtx.pt) == AABB.ContainmentType.Disjoint)
                            continue;

                        Vector3 ptdir = Vector3.Normalize(vtx.pt - e.v0.pt);
                        if ((ptdir - e.dir).LengthSquared() < 0.01f)
                        {
                            float len = (vtx.pt - e.v0.pt).Length();
                            if (len < elen)
                            {
                                //System.Diagnostics.Debug.WriteLine("found");
                            }
                        }

                    }
                }
            }

            public void FixWindings()
            {
                if (faces.Count == 0)
                    return;
                foreach (Face f in faces)
                {
                    f.visited = false;
                }
                FixWindingsRecursive(faces[0]);
            }

            void FixWindingsRecursive(Face f)
            {
                if (f.visited)
                    return;
                f.visited = true;
            }

            bool epsEq(float a, float b)
            {
                const float epsilon = 0.001f;
                float e = a - b;
                return (e > -epsilon && e < epsilon);
            }
            bool epsEq(Vector3 a, Vector3 v)
            {
                return epsEq(a.X, v.X) &&
                        epsEq(a.Y, v.Y) &&
                        epsEq(a.Z, v.Z);
            }


            bool IsUnique(KdTree<float, Vector3> kd, Vector3 pt)
            {
                var result = kd.GetNearestNeighbours(new float[] { pt.X, pt.Y, pt.Z }, 1);
                if (result.Count() > 0 &&
                    epsEq(result[0].Value, pt))
                {
                    return false;
                }
                kd.Add(new float[] { pt.X, pt.Y, pt.Z }, pt);
                return true;
            }

            Vector3 NearestPt(Vector3 linePt1, Vector3 linePt2, Vector3 testPt)
            {
                Vector3 lineDir = (linePt2 - linePt1);
                float lineDistAlong = Vector3.Dot(testPt - linePt1, lineDir) / lineDir.LengthSquared();
                if (lineDistAlong < 0)
                    return linePt1;
                else if (lineDistAlong > 1)
                    return linePt2;
                return linePt1 + lineDir * lineDistAlong;
            }

        
            public List<Vector3> GetRStuds(Vector3 []candidates, List<Tuple<Vector3, Vector3>> bisectors)
            {
                List<Vector3> outPts = new List<Vector3>();
                foreach (var kv in this.edgeDict)
                {
                    kv.Value.flag = 0;
                }
                List<Edge> edges = new List<Edge>();
                List<Vector3> candidatePts = new List<Vector3>();                
                {
                    KdTree<float, Vector3> kd = new KdTree<float, Vector3>(3, new KdTree.Math.FloatMath());
                    GetBottomEdges(edges);

                    if (edges.Count == 0)
                        return outPts;

                    Vector3 planeNormal = new Vector3(0, 1, 0);
                    float planeDist = Vector3.Dot(edges[0].v0.pt, planeNormal);
                    foreach (Vector3 c in candidates)
                    {
                        Vector3 pt = c - planeNormal * Vector3.Dot(c, planeNormal);
                        pt += planeNormal * planeDist;
                        if (IsUnique(kd, pt))
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
                            float sinTheta = Vector3.Cross(bisectAngle, v1adir).Length();
                            Vector3 testPt = v0 + (6.0f / sinTheta) * bisectAngle;
                            bisectors.Add(new Tuple<Vector3, Vector3>(v0, testPt)); 
                            if (IsUnique(kd, testPt))
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
                            float sinTheta = Vector3.Cross(bisectAngle, v1adir).Length();
                            Vector3 testPt = v0 + (6.0f / sinTheta) * bisectAngle;
                            bisectors.Add(new Tuple<Vector3, Vector3>(v0, testPt));
                            if (IsUnique(kd, testPt))
                                candidatePts.Add(testPt);
                        }

                        if (epsEq(edge.len, 12))
                        {
                            Vector3 edgeNrm = Vector3.Cross(planeNormal, edge.dir);
                            Vector3 pt1 = (edge.v0.pt + edge.v1.pt) * 0.5f +
                                edgeNrm * 6.0f;
                            if (IsUnique(kd, pt1))
                                candidatePts.Add(pt1);
                            Vector3 pt2 = (edge.v0.pt + edge.v1.pt) * 0.5f -
                                edgeNrm * 6.0f;
                            if (IsUnique(kd, pt2))
                                candidatePts.Add(pt2);
                        }
                    }
                }

                foreach (Vector3 cpt in candidatePts)
                {
                    List<Edge> touchedEdges = new List<Edge>();
                    bool hasBlockage = false;
                    foreach (var e in edges)
                    {
                        Vector3 nearestPt = NearestPt(e.v0.pt, e.v1.pt, cpt);

                        float lenSq = (nearestPt - cpt).LengthSquared();
                        if (lenSq > 34 && lenSq < 38)
                        {
                            touchedEdges.Add(e);
                        }
                        else if (lenSq < 34)
                            hasBlockage = true;
                    }

                    if (hasBlockage)
                        continue;
                    if (touchedEdges.Count() >= 3)
                        outPts.Add(cpt);
                }

                return outPts;
            }

            public void GetBottomEdges(List<Edge> edges)
            {
                AABB aabb = AABB.CreateFromPoints(this.vertices.Select(v => v.pt).ToList());
                float maxy = aabb.Max.Y;

                foreach (var e in this.edgeDict.Values)
                {
                    if (epsEq(e.len, 12))
                    {

                    }
                }
                foreach (var e in this.edgeDict.Values)
                {
                    if (epsEq(e.v0.pt.Y, maxy) && epsEq(e.v1.pt.Y, maxy))
                        edges.Add(e);
                }
            }

            public void GetVertices(List<Vtx> vlist)
            {
                foreach (var f in faces)
                {
                    Vector3 nrm = f.Normal;
                    List<Vertex> vl = new List<Vertex>();
                    f.GetVertices(vl);
                    Vtx[] vtxs = vl.Select(v => new Vtx(v.pt, nrm, new Vector2(f.isinvalid ? 1 : 0, 0))).ToArray();
                    vlist.Add(vtxs[0]);
                    vlist.Add(vtxs[1]);
                    vlist.Add(vtxs[2]);
                    if (vtxs.Length == 4)
                    {
                        vlist.Add(vtxs[0]);
                        vlist.Add(vtxs[2]);
                        vlist.Add(vtxs[3]);
                    }
                }
            }
            void RemoveDuplicateFaces()
            {
                Dictionary<ulong, Face> faceHashes = new Dictionary<ulong, Face>();
                for (int idx = 0; idx < faces.Count;)
                {
                    Face f = faces[idx];
                    ulong faceHash = f.ComputeHash();
                    if (faceHashes.ContainsKey(faceHash))
                    {
                        faces.RemoveAt(idx);
                    }
                    else
                    {
                        faceHashes.Add(faceHash, f);
                        ++idx;
                    }
                }
            }
            void ClearFound()
            {
                foreach (Face f in faces)
                {
                    foreach (EdgePtr e in f.edges)
                    {
                        e.e.flag = 0;
                    }
                }
            }

            public List<Loop> FindSquares()
            {
                List<Loop> foundLoops = new List<Loop>();
                foreach (Face f in faces)
                {
                    foreach (EdgePtr eptr in f.edges)
                    {
                        Edge e = eptr.e;
                        if (e.flag == 0 && e.len == 12)
                        {
                            List<Edge> edges = new List<Edge>() { e };
                            if (FollowEdge(edges))
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
        }
    }
}