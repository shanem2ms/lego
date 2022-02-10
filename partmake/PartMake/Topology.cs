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
        public abstract class INode
        {
            public virtual IEnumerable<INode> Children { get; }
            public bool IsSelected { get; set; } = false;

            public INode FindSelected()
            {
                if (IsSelected)
                    return this;
                else if (Children != null)
                {
                    foreach (INode child in Children)
                    {
                        INode sel = child.FindSelected();
                        if (sel != null)
                            return sel;
                    }
                }
                return null;
            }
        }
        public class Vertex : INode
        {
            public int idx;
            public Vector3 pt;
            public List<Edge> edges =
                new List<Edge>();
            public int errorFlags = 0;

            public override IEnumerable<INode> Children => null;

            public override string ToString()
            {
                return String.Format("{0} [{1}, {2}, {3}]", idx, pt.X, pt.Y, pt.Z);
            }

            public IEnumerable<Face> GetFaces()
            {
                Dictionary<ulong, Face> faceDict = new Dictionary<ulong, Face>();
                foreach (Edge e in edges)
                {
                    foreach (var eptr in e.edgePtrs)
                    {
                        ulong hash = eptr.parentFace.ComputeHash();
                        if (!faceDict.ContainsKey(hash))
                        {
                            faceDict.Add(hash, eptr.parentFace);
                        }
                    }
                }

                return faceDict.Values;
            }
        }

        public static class Eps
        {
            public static bool Eq(float a, float b)
            {
                const float epsilon = 0.001f;
                float e = a - b;
                return (e > -epsilon && e < epsilon);
            }

            public static bool Eq(Vector3 a, Vector3 v)
            {
                return Eq(a.X, v.X) &&
                        Eq(a.Y, v.Y) &&
                        Eq(a.Z, v.Z);
            }
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
                v0.edges.Add(this);
                v1.edges.Add(this);
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
            public int errorFlags = 0;

            public bool IsConnected(Edge other)
            {
                if (other.v0.idx == v0.idx || other.v0.idx == v1.idx ||
                    other.v1.idx == v0.idx || other.v1.idx == v1.idx)
                    return true;
                return false;
            }
            public float DistanceFromPt(Vector3 p)
            {
                Vector3 v = v0.pt;
                Vector3 w = v1.pt;
                // Return minimum distance between line segment vw and point p
                float l2 = Vector3.DistanceSquared(v, w);  // i.e. |w-v|^2 -  avoid a sqrt
                if (l2 == 0.0) return Vector3.Distance(p, v);   // v == w case
                                                                // Consider the line extending the segment, parameterized as v + t (w - v).
                                                                // We find projection of point p onto the line. 
                                                                // It falls where t = [(p-v) . (w-v)] / |w-v|^2
                                                                // We clamp t from [0,1] to handle points outside the segment vw.
                float t = Math.Max(0, Math.Min(1, Vector3.Dot(p - v, w - v) / l2));
                Vector3 projection = v + t * (w - v);  // Projection falls on the segment
                return Vector3.DistanceSquared(p, projection);
            }

            public static ulong ComputeHash(Vertex _v0, Vertex _v1)
            {
                UInt64 hashedValue = 3074457345618258791ul;
                hashedValue += (ulong)_v0.idx;
                hashedValue *= 3074457345618258799ul;
                hashedValue += (ulong)_v1.idx;
                hashedValue *= 3074457345618258799ul;
                return hashedValue;
            }
            public ulong ComputeHash()
            {
                return ComputeHash(v0, v1);
            }

            public bool ContainsVertexIdx(int idx)
            {
                return v0.idx == idx || v1.idx == idx;
            }
        }

        public class EdgePtr : INode
        {
            public Edge e;
            public bool reverse;
            public Face parentFace;
            public Vector3 Dir => reverse ? -e.dir : e.dir;
            public Vertex V0 => reverse ? e.v1 : e.v0;
            public Vertex V1 => reverse ? e.v0 : e.v1;
            public override IEnumerable<INode> Children => new Vertex[] { V0, V1 };

            public EdgePtr(Edge _e, bool _reverse, Face _parent)
            {
                e = _e;
                reverse = _reverse;
                parentFace = _parent;
                e.edgePtrs.Add(this);
            }
            public EdgePtr(Vertex _v0, Vertex _v1, Dictionary<ulong, Edge> edgeDict,
                Face _parentFace)
            {
                this.parentFace = _parentFace;
                ulong hash;
                if (_v0.idx < _v1.idx)
                {
                    this.reverse = false;
                    hash = Edge.ComputeHash(_v0, _v1);
                }
                else
                {
                    this.reverse = true;
                    hash = Edge.ComputeHash(_v1, _v0);
                }
                if (!edgeDict.TryGetValue(hash, out e))
                {
                    e = new Edge(_v0, _v1);
                    if (e.v0.idx == 166 && e.v1.idx == 169)
                        Debug.Write("A");
                    edgeDict.Add(hash, e);
                }
                e.edgePtrs.Add(this);
            }

            public override string ToString()
            {
                return String.Format("Edge [{0} {1}]", V0.idx, V1.idx);
            }
        }

        public class Face : INode
        {
            public List<EdgePtr> edges;
            public bool visited;
            public bool isinvalid = false;
            public AABB aabb;
            public string id;
            public IEnumerable<Vertex> Vtx => edges.Select(e => e.V0);

            public List<Edge> nonmfEdges;

            float Area(Vector2 d0, Vector2 d1, Vector2 d2)
            {
                float dArea = ((d1.X - d0.X) * (d2.Y - d0.Y) - (d2.X - d0.X) * (d1.Y - d0.Y)) / 2.0f;
                return (dArea > 0.0) ? dArea : -dArea;
            }

            public float Area()
            {
                Vector3 nrm = Normal;
                Vector3 xdir = edges[0].Dir;
                Vector3 ydir = Vector3.Cross(xdir, nrm);

                Vector2 p1 = new Vector2(Vector3.Dot(edges[0].V0.pt, xdir),
                    Vector3.Dot(edges[0].V0.pt, ydir));
                Vector2 p2 = new Vector2(Vector3.Dot(edges[1].V0.pt, xdir),
                    Vector3.Dot(edges[1].V0.pt, ydir));
                Vector2 p3 = new Vector2(Vector3.Dot(edges[2].V0.pt, xdir),
                    Vector3.Dot(edges[2].V0.pt, ydir));
                return Area(p1, p2, p3);
            }
            public static bool CheckDuplicateEdges(List<EdgePtr> _edges)
            {
                List<Edge> checkEdges = _edges.Select(e => e.e).ToList();
                checkEdges.Sort((a, b) => (a.ComputeHash().CompareTo(b.ComputeHash())));
                for (int i = 1; i < checkEdges.Count(); i++)
                {
                    if (checkEdges[i - 1].ComputeHash() == checkEdges[i].ComputeHash())
                    {
                        //Debug.WriteLine(String.Format("Dupe {0}", id));
                        return true;
                        //Debugger.Break();
                        //_edges.Remove(_edges.First(e => e.e == checkEdges[i]));
                    }
                }
                return false;
            }
            public Face(string _id, List<EdgePtr> _edges)
            {
                id = _id;
                edges = _edges;
                if (CheckDuplicateEdges(edges))
                {
                    Debug.WriteLine(string.Format("Dupe {0}", id));
                }
                aabb = AABB.CreateFromPoints(edges.Select(e => e.V0.pt));
                foreach (var e in edges)
                {
                    e.parentFace = this;
                }
                FixEdgeOrder();
            }

            public bool HasVertex(Vertex _vtx)
            {
                var vtx = Vtx;
                foreach (Vertex v in vtx)
                {
                    if (v.idx == _vtx.idx)
                        return true;
                }
                return false;
            }

            public bool ContainsVertex(int idx)
            {
                return Vtx.Any(v => v.idx == idx);
            }

            public bool IsPointInsideTriangle(Vertex vtx)
            {
                if (ContainsVertex(vtx.idx))
                    return false;
                Vector3 pt = vtx.pt;
                Vector3 nrm = Normal;
                if (!Eps.Eq(Vector3.Dot(pt - edges[0].V0.pt, nrm), 0))
                    return false;
                Vector3 xdir = edges[0].Dir;
                Vector3 ydir = Vector3.Cross(xdir, nrm);

                Vector2 p1 = new Vector2(Vector3.Dot(edges[0].V0.pt, xdir),
                    Vector3.Dot(edges[0].V0.pt, ydir));
                Vector2 p2 = new Vector2(Vector3.Dot(edges[1].V0.pt, xdir),
                    Vector3.Dot(edges[1].V0.pt, ydir));
                Vector2 p3 = new Vector2(Vector3.Dot(edges[2].V0.pt, xdir),
                    Vector3.Dot(edges[2].V0.pt, ydir));
                Vector2 p = new Vector2(Vector3.Dot(pt, xdir), Vector3.Dot(pt, ydir));
                float alpha = ((p2.Y - p3.Y) * (p.X - p3.X) + (p3.X - p2.X) * (p.Y - p3.Y)) /
                        ((p2.Y - p3.Y) * (p1.X - p3.X) + (p3.X - p2.X) * (p1.Y - p3.Y));
                float beta = ((p3.Y - p1.Y) * (p.X - p3.X) + (p1.X - p3.X) * (p.Y - p3.Y)) /
                       ((p2.Y - p3.Y) * (p1.X - p3.X) + (p3.X - p2.X) * (p1.Y - p3.Y));
                float gamma = 1.0f - alpha - beta;

                return alpha > 0 && beta > 0 && gamma > 0;
            }
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

            public void FixEdgeOrder()
            {
                List<EdgePtr> newEdges = new List<EdgePtr>();
                newEdges.Add(edges[0]);
                Vertex curVtx = edges[0].V1;
                edges.RemoveAt(0);
                while (edges.Count() > 0)
                {
                    foreach (EdgePtr e in edges)
                    {
                        if (e.V0.idx == curVtx.idx)
                        {
                            curVtx = e.V1;
                            newEdges.Add(e);
                            edges.Remove(e);
                            break;
                        }
                        else if (e.V1.idx == curVtx.idx)
                        {
                            curVtx = e.V0;
                            e.reverse = !e.reverse;
                            newEdges.Add(e);
                            edges.Remove(e);
                            break;
                        }
                    }
                }

                edges = newEdges;
                EdgePtr lastEdge = edges.Last();
                if (lastEdge.V1.idx != edges[0].V0.idx)
                    lastEdge.reverse = !lastEdge.reverse;
            }

            public override string ToString()
            {
                return edges.Count() == 4 ? "Quad" : "Tri";
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

            bool calcNormal = false;
            Vector3 nrm;
            public Vector3 Normal
            {
                get
                {
                    if (!calcNormal)
                    {
                        for (int idx = 1; idx < edges.Count(); ++idx)
                        {
                            if (Vector3.Dot(edges[0].Dir, edges[idx].Dir) < 0.99f)
                            {
                                nrm = Vector3.Normalize(Vector3.Cross(edges[0].Dir, edges[idx].Dir));
                                calcNormal = true;
                                break;
                            }
                        }
                        if (!calcNormal)
                            Debugger.Break();
                    }
                    return nrm;
                }
            }

            public override IEnumerable<INode> Children => edges;
        }
        public class Mesh
        {
            public static float Epsilon = 0.001f;
            public KdTree<float, Vertex> kdTree = new KdTree<float, Vertex>(3, new KdTree.Math.FloatMath());
            List<Vertex> vertices = new List<Vertex>();
            public List<Face> faces = new List<Face>();
            int nextVtxIdx = 0;
            public Dictionary<ulong, Edge> edgeDict = new Dictionary<ulong, Edge>();

            public static bool IsEqual(float[] vals, Vector3 v2)
            {
                Vector3 v1 = new Vector3(vals[0], vals[1], vals[2]);
                return Vector3.DistanceSquared(v1, v2) < Epsilon;
            }

            public List<Face> FacesFromId(string id)
            {
                return this.faces.Where(f => f.id == id).ToList();
            }

            public Vertex AddVertex(Vector3 v)
            {
                var nodes = kdTree.GetNearestNeighbours(new float[] { v.X, v.Y, v.Z }, 1);
                if (nodes.Length > 0 && IsEqual(nodes[0].Point, v))
                {
                    return nodes[0].Value;
                }
                else
                {
                    Vertex nv = new Vertex() { pt = v, idx = nextVtxIdx++ };
                    kdTree.Add(new float[] { v.X, v.Y, v.Z }, nv);
                    return nv;
                }
            }
            public Face AddFace(string id, List<Vector3> vertices)
            {
                List<Vertex> verlist = new List<Vertex>();
                foreach (var v in vertices)
                {
                    verlist.Add(AddVertex(v));
                }
                List<EdgePtr> elist = new List<EdgePtr>();
                for (int idx = 0; idx < verlist.Count; ++idx)
                {
                    Vertex v0 = verlist[idx];
                    Vertex v1 = verlist[(idx + 1) % verlist.Count];

                    EdgePtr eptr = new EdgePtr(v0, v1, edgeDict, null);
                    elist.Add(eptr);
                }

                Face f = new Face(id, elist);
                foreach (EdgePtr eptr in f.edges)
                {
                    eptr.parentFace = f;
                }
                this.vertices.AddRange(verlist);
                this.faces.Add(f);

                return f;
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
                Triangulate();
                SplitXJunctions();
                //SplitTJunctions();
                SplitInteriorEdges();
                RemoveDuplicateFaces();
                FixWindings();
            }

            void SplitInteriorEdges()
            {
                bool didSplit = false;
                do
                {
                    didSplit = false;
                    List<Edge> nonMFEdges = new List<Edge>();
                    GetNonManifold(nonMFEdges);
                    foreach (Edge e in nonMFEdges)
                    {
                        {
                            var faces = e.v0.GetFaces();
                            foreach (Face f in faces)
                            {
                                if (f.IsPointInsideTriangle(e.v1))
                                {
                                    SplitFaceOnInteriorPoint(f, e.v1);
                                    didSplit = true;
                                    Debug.Write("Pt inside");
                                }
                            }
                        }
                        {
                            var faces = e.v1.GetFaces();
                            foreach (Face f in faces)
                            {
                                if (f.IsPointInsideTriangle(e.v0))
                                {
                                    SplitFaceOnInteriorPoint(f, e.v0);
                                    didSplit = true;
                                    Debug.Write("Pt inside");
                                }
                            }
                        }

                    }
                } while (didSplit);
            }

            void Triangulate()
            {
                List<Face> newFaces = new List<Face>();
                foreach (Face f in faces)
                {
                    if (f.Vtx.Count() == 3)
                        newFaces.Add(f);
                    else
                    {
                        Face fa, fb;
                        TriangulateQuad(f, out fa, out fb);
                        newFaces.Add(fa);
                        newFaces.Add(fb);
                    }
                }
                this.faces = newFaces;
            }

            void TriangulateQuad(Face f, out Face fa, out Face fb)
            {
                EdgePtr e0a = f.edges[0];
                EdgePtr e1a = f.edges[1];
                EdgePtr e2a = new EdgePtr(e1a.V1, e0a.V0, edgeDict, null);
                fa = new Face(f.id, new List<EdgePtr> { e0a, e1a, e2a });
                EdgePtr e0b = f.edges[2];
                EdgePtr e1b = f.edges[3];
                EdgePtr e2b = new EdgePtr(e1b.V1, e0b.V0, edgeDict, null);
                fb = new Face(f.id, new List<EdgePtr> { e0b, e1b, e2b });
            }

            void SplitXJunctions()
            {
                bool didSplit = false;
                do
                {
                    didSplit = false;
                    List<Edge> nonMFEdges = new List<Edge>();
                    GetNonManifold(nonMFEdges);
                    foreach (Edge e in nonMFEdges)
                    {
                        Vector3 dir = e.dir;
                        foreach (Face f in faces)
                        {
                            if (!Eps.Eq(Vector3.Dot(e.dir, f.Normal), 0))
                                continue;
                            if (e.edgePtrs.Any(e => e.parentFace == f))
                                continue;

                            if (f.IsPointInsideTriangle(e.v0) && !f.IsPointInsideTriangle(e.v1))
                            {
                                if (SplitFaceOnEdge(f, e))
                                {
                                    didSplit = true;
                                    break;
                                }
                            }
                        }
                    }
                } while (didSplit);
            }

            // Returns 1 if the lines intersect, otherwise 0. In addition, if the lines 
            // intersect the intersection point may be stored in the floats i.X and i_y.
            static bool GetLineIntersection(Vector2 p0, Vector2 p1,
                Vector2 p2, Vector2 p3, out float ot)
            {
                Vector2 s1, s2;
                s1.X = p1.X - p0.X; s1.Y = p1.Y - p0.Y;
                s2.X = p3.X - p2.X; s2.Y = p3.Y - p2.Y;

                float s, t;
                s = (-s1.Y * (p0.X - p2.X) + s1.X * (p0.Y - p2.Y)) / (-s2.X * s1.Y + s1.X * s2.Y);
                t = (s2.X * (p0.Y - p2.Y) - s2.Y * (p0.X - p2.X)) / (-s2.X * s1.Y + s1.X * s2.Y);

                if (s >= 0 && s <= 1 && t >= 0 && t <= 1)
                {
                    ot = t;
                    return true;
                }

                ot = -1;
                return false; // No collision
            }
            void SplitFaceOnInteriorPoint(Face f, Vertex v)
            {
                foreach (var eptr in f.edges)
                {
                    eptr.e.edgePtrs.Remove(eptr);
                }
                this.faces.Remove(f);
                var vtx = f.Vtx.ToArray();

                for (int i = 0; i < 3; ++i)
                {
                    Face f1 = new Face(f.id, new List<EdgePtr> { new EdgePtr(v, vtx[i], this.edgeDict, null),
                        new EdgePtr(vtx[i], vtx[(i + 1) % 3], this.edgeDict, null),
                        new EdgePtr(vtx[(i + 1) % 3], v, this.edgeDict, null) });
                    this.faces.Add(f1);
                }
            }
            
            bool SplitFaceOnEdge(Face f, Edge e)
            {
                Vector3 xdir = f.edges[0].Dir;
                Vector3 ydir = Vector3.Cross(xdir, f.Normal);

                Vector2 p1 = new Vector2(Vector3.Dot(f.edges[0].V0.pt, xdir),
                    Vector3.Dot(f.edges[0].V0.pt, ydir));
                Vector2 p2 = new Vector2(Vector3.Dot(f.edges[1].V0.pt, xdir),
                    Vector3.Dot(f.edges[1].V0.pt, ydir));
                Vector2 p3 = new Vector2(Vector3.Dot(f.edges[2].V0.pt, xdir),
                    Vector3.Dot(f.edges[2].V0.pt, ydir));
                Vector2 ep0 = new Vector2(Vector3.Dot(e.v0.pt, xdir), Vector3.Dot(e.v0.pt, ydir));
                Vector2 ep1 = new Vector2(Vector3.Dot(e.v1.pt, xdir), Vector3.Dot(e.v1.pt, ydir));

                Debug.Write("split ");
                float t;
                if (GetLineIntersection(p1, p2, ep0, ep1, out t) && t > 0.01 && t < 0.99)
                {
                    Vector3 newpt = f.edges[0].V0.pt + (f.edges[0].V1.pt - f.edges[0].V0.pt) * t;
                    Vertex nv = AddVertex(newpt);
                    float dist = e.DistanceFromPt(newpt);
                    SplitEdge(f.edges[0].e, nv);
                    if (!e.ContainsVertexIdx(nv.idx))
                        SplitEdge(e, nv);
                    Debug.Write("A\n");
                    return true;
                }
                if (GetLineIntersection(p2, p3, ep0, ep1, out t) && t > 0.01 && t < 0.99)
                {
                    Vector3 newpt = f.edges[1].V0.pt + (f.edges[1].V1.pt - f.edges[1].V0.pt) * t;
                    Vertex nv = AddVertex(newpt);
                    float dist = e.DistanceFromPt(newpt);
                    SplitEdge(f.edges[1].e, nv);
                    if (!e.ContainsVertexIdx(nv.idx))
                        SplitEdge(e, nv);
                    Debug.Write("B\n");
                    return true;
                }
                if (GetLineIntersection(p3, p1, ep0, ep1, out t) && t > 0.01 && t < 0.99)
                {
                    Vector3 newpt = f.edges[2].V0.pt + (f.edges[2].V1.pt - f.edges[2].V0.pt) * t;
                    Vertex nv = AddVertex(newpt);
                    float dist = e.DistanceFromPt(newpt);
                    SplitEdge(f.edges[2].e, nv);
                    if (!e.ContainsVertexIdx(nv.idx))
                        SplitEdge(e, nv);
                    Debug.Write("C\n");
                    return true;
                }

                return false;
            }
            void SplitTJunctions()
            {
                bool edgesSplit = false;
                do
                {
                    edgesSplit = false;
                    List<Edge> allEdges = new List<Edge>(edgeDict.Values);
                    foreach (var e in allEdges)
                    {
                        AABB aabb = AABB.CreateFromPoints(new List<Vector3>()
                    { e.v0.pt, e.v1.pt });
                        float elen = (e.v1.pt - e.v0.pt).Length();

                        foreach (var vtx in vertices)
                        {
                            if (aabb.Contains(vtx.pt) == AABB.ContainmentType.Disjoint)
                                continue;

                            Vector3 ptdir1 = Vector3.Normalize(vtx.pt - e.v0.pt);
                            Vector3 ptdir2 = Vector3.Normalize(e.v1.pt - vtx.pt);
                            if ((ptdir1 - e.dir).LengthSquared() < 0.001f &&
                                (ptdir2 - e.dir).LengthSquared() < 0.001f)
                            {
                                float len = (vtx.pt - e.v0.pt).Length();
                                if (len < elen)
                                {
                                    Debug.WriteLine(String.Format("Split Edge [{0} {1}-{2}]", vtx.idx, e.v0.idx, e.v1.idx));
                                    SplitEdge(e, vtx);
                                    edgesSplit = true;
                                }
                            }

                        }
                    }
                } while (edgesSplit);
            }
            bool reported = false;
            void SplitEdge(Edge e, Vertex vtx)
            {
                List<Face> splitFaces = new List<Face>();
                e.v0.edges.Remove(e);
                e.v1.edges.Remove(e);
                List<EdgePtr> eptrs = new List<EdgePtr>(e.edgePtrs);
                foreach (EdgePtr ce in eptrs)
                {
                    Face f = ce.parentFace;
                    float area = f.Area();
                    if (f.ContainsVertex(vtx.idx))
                    {
                        vtx.errorFlags |= 1;
                        continue;
                    }
                    int epIdx = f.edges.IndexOf(ce);
                    f.edges.RemoveAt(epIdx);
                    EdgePtr e1 = new EdgePtr(e.v0, vtx, edgeDict, f);
                    EdgePtr e2 = new EdgePtr(vtx, e.v1, edgeDict, f);
                    f.edges.Add(e1);
                    f.edges.Insert(0, e2);
                    if (Face.CheckDuplicateEdges(f.edges))
                        Debug.WriteLine("Dupe edge");
                    splitFaces.Add(f);
                }
                e.edgePtrs.Clear();
                if (e.v0.idx == 166 && e.v1.idx == 169)
                    Debug.WriteLine("H");
                this.edgeDict.Remove(e.ComputeHash());

                foreach (Face f in splitFaces)
                {
                    f.FixEdgeOrder();
                    Face fa, fb;
                    TriangulateQuad(f, out fa, out fb);
                    this.faces.Remove(f);
                    this.faces.Add(fa);
                    this.faces.Add(fb);
                }
            }

            public void GetEdges(List<Edge> edges)
            {
                foreach (Edge e in edgeDict.Values)
                {
                    edges.Add(e);
                }
            }
            public void GetNonManifold(List<Edge> edges)
            {
                foreach (Edge e in edgeDict.Values)
                {
                    if (e.edgePtrs.Count < 2)
                        edges.Add(e);
                }
            }
            public void GetNonManifold2(List<Edge> edges)
            {
                foreach (Edge e in edgeDict.Values)
                {
                    if (e.edgePtrs.Count > 2)
                        edges.Add(e);
                }
            }

            public void GetSelectedEdges(List<Edge> e)
            {
                INode selected = null;
                foreach (Face f in faces)
                {
                    selected = f.FindSelected();
                    if (selected != null)
                        break;
                }

                if (selected == null)
                    return;
                if (selected is Face)
                {
                    e.AddRange((selected as Face).edges.Select(eptr => eptr.e));
                }
                else if (selected is EdgePtr)
                {
                    e.Add((selected as EdgePtr).e);
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
                foreach (var eptr in f.edges)
                {
                }
            }


            bool IsUnique(KdTree<float, Vector3> kd, Vector3 pt)
            {
                var result = kd.GetNearestNeighbours(new float[] { pt.X, pt.Y, pt.Z }, 1);
                if (result.Count() > 0 &&
                    Eps.Eq(result[0].Value, pt))
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


            public List<Vector3> GetRStuds(Vector3[] candidates, List<Tuple<Vector3, Vector3>> bisectors)
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

                        if (Eps.Eq(edge.len, 12))
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

            bool FollowCoplanarEdges(List<Edge> edges, Vector3 nrm)
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

            public List<Loop> FindLoops()
            {
                List<Loop> foundLoops = new List<Loop>();
                foreach (Face f in faces)
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

            public void GetBottomEdges(List<Edge> edges)
            {
                AABB aabb = AABB.CreateFromPoints(this.vertices.Select(v => v.pt).ToList());
                float maxy = aabb.Max.Y;

                foreach (var e in this.edgeDict.Values)
                {
                    if (Eps.Eq(e.len, 12))
                    {

                    }
                }
                foreach (var e in this.edgeDict.Values)
                {
                    if (Eps.Eq(e.v0.pt.Y, maxy) && Eps.Eq(e.v1.pt.Y, maxy))
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