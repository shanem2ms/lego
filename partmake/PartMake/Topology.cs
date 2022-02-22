using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.DoubleNumerics;
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
            public double mindist = -1;

            public override IEnumerable<INode> Children => null;

            public override string ToString()
            {
                return String.Format("{0} [{1}, {2}, {3}] {4}", idx, pt.X, pt.Y, pt.Z, mindist);
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
            public static bool Eq(double a, double b)
            {
                double e = a - b;
                return (e > -Mesh.Epsilon && e < Mesh.Epsilon);
            }

            public static bool Eq(Vector3 a, Vector3 v)
            {
                return (a - v).LengthSquared() < (Mesh.Epsilon * 4);
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

        public class Edge : INode
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

            public Vector3 Interp(double t)
            {
                return v0.pt + (v1.pt - v0.pt) * t;
            }
            public Vertex v0 { get; set; }
            public Vertex v1 { get; set; }
            public Vector3 dir { get; set; }

            public IEnumerable<Face> Faces => edgePtrs.Select(eptr => eptr.parentFace);
            public double len { get; set; }
            public int flag; // for processing
            public bool instack = false;
            public List<EdgePtr> edgePtrs =
                new List<EdgePtr>();
            public AABB aabb;
            public Edge[] splitEdges = null;
            public int errorFlags { get; set; } = 0;
            public bool split => splitEdges != null;

            public int FaceCount => edgePtrs.Count();
            public bool IsConnected(Edge other)
            {
                if (other.v0.idx == v0.idx || other.v0.idx == v1.idx ||
                    other.v1.idx == v0.idx || other.v1.idx == v1.idx)
                    return true;
                return false;
            }
            public double DistanceFromPt(Vector3 p)
            {
                Vector3 v = v0.pt;
                Vector3 w = v1.pt;
                // Return minimum distance between line segment vw and point p
                double l2 = Vector3.DistanceSquared(v, w);  // i.e. |w-v|^2 -  avoid a sqrt
                if (l2 == 0.0) return Vector3.Distance(p, v);   // v == w case
                                                                // Consider the line extending the segment, parameterized as v + t (w - v).
                                                                // We find projection of point p onto the line. 
                                                                // It falls where t = [(p-v) . (w-v)] / |w-v|^2
                                                                // We clamp t from [0,1] to handle points outside the segment vw.
                double t = Math.Max(0, Math.Min(1, Vector3.Dot(p - v, w - v) / l2));
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
            
            public override string ToString()
            {
                return String.Format("Edge [{0} {1}]", v0.idx, v1.idx);
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
                Face _parentFace, Mesh.LogDel logdel)
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
                    edgeDict.Add(hash, e);
                    if (e.len < 0.01f)
                        logdel(String.Format("Small edge {0}, {1}", e, e.len));
                }
                if (e.split)
                {                    
                    throw new Exception("Reintroducing split edge");
                }
                e.edgePtrs.Add(this);
            }

            public bool IsValid()
            {
                return V0.idx != V1.idx;
            }

            public override string ToString()
            {
                return String.Format("Edge [{0} {1}]", V0.idx, V1.idx);
            }
        }

        public class Face : INode
        {
            public List<EdgePtr> edges { get; set; }
            public bool visited;
            public bool isexterior;
            public bool isinvalid = false;
            public AABB aabb;
            public string id;
            public int idx { get; set; }
            public IEnumerable<Vertex> Vtx => edges.Select(e => e.V0);

            public List<Edge> nonmfEdges;

            public bool IsValid()
            {
                foreach (var e in edges)
                {
                    if (!e.IsValid())
                        return false;
                }
                double d1 = Vector3.Dot(edges[0].Dir, edges[1].Dir);
                double d2 = Vector3.Dot(edges[1].Dir, edges[2].Dir);
                double d3 = Vector3.Dot(edges[2].Dir, edges[0].Dir);
                if (Eps.Eq(d1, 1) || Eps.Eq(d1, -1) ||
                    Eps.Eq(d2, 1) || Eps.Eq(d2, -1) ||
                    Eps.Eq(d3, 1) || Eps.Eq(d3, -1))
                    return false;
                return true;
            }

            static double Area(Vector2 d0, Vector2 d1, Vector2 d2)
            {
                double dArea = ((d1.X - d0.X) * (d2.Y - d0.Y) - (d2.X - d0.X) * (d1.Y - d0.Y)) / 2.0f;
                return (dArea > 0.0) ? dArea : -dArea;
            }

            public double Area()
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

            public void ReverseWinding()
            {
                this.edges.Reverse();
                foreach (var eptr in this.edges)
                {
                    eptr.reverse = !eptr.reverse;
                }
                calcNormal = false;
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
                return !ContainsVertex(vtx.idx) && IsPointOnTriangle(vtx);
            }


            public bool IsPointOnTriangle(Vertex vtx)
            {
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
                double alpha = ((p2.Y - p3.Y) * (p.X - p3.X) + (p3.X - p2.X) * (p.Y - p3.Y)) /
                        ((p2.Y - p3.Y) * (p1.X - p3.X) + (p3.X - p2.X) * (p1.Y - p3.Y));
                double beta = ((p3.Y - p1.Y) * (p.X - p3.X) + (p1.X - p3.X) * (p.Y - p3.Y)) /
                       ((p2.Y - p3.Y) * (p1.X - p3.X) + (p3.X - p2.X) * (p1.Y - p3.Y));
                double gamma = 1.0f - alpha - beta;

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
                    bool removed = false;
                    foreach (EdgePtr e in edges)
                    {
                        if (e.V0.idx == curVtx.idx)
                        {
                            curVtx = e.V1;
                            newEdges.Add(e);
                            edges.Remove(e);
                            removed = true;
                            break;
                        }
                        else if (e.V1.idx == curVtx.idx)
                        {
                            curVtx = e.V0;
                            e.reverse = !e.reverse;
                            newEdges.Add(e);
                            edges.Remove(e);
                            removed = true;
                            break;
                        }
                    }
                    if (!removed)
                    {
                        newEdges.Add(edges[0]);
                        edges.Remove(edges[0]);
                    }
                }

                edges = newEdges;
                EdgePtr lastEdge = edges.Last();
                if (lastEdge.V1.idx != edges[0].V0.idx)
                    lastEdge.reverse = !lastEdge.reverse;
            }

            public override string ToString()
            {
                return 
                    string.Join(' ', edges.Select(e => e.V0.idx.ToString()).ToArray());
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
            public static double Epsilon = 0.00001f;
            public KdTree<double, Vertex> kdTree = new KdTree<double, Vertex>(3, new KdTree.Math.DoubleMath());
            List<Vertex> vertices = new List<Vertex>();
            public List<Face> faces = new List<Face>();
            int nextVtxIdx = 0;
            public Dictionary<ulong, Edge> edgeDict = new Dictionary<ulong, Edge>();
            double vertexMinDist = 0.0005;
            EdgeIntersectCPP edgeIntersect = new EdgeIntersectCPP();
            public List<string> logLines = new List<string>();

            string log = null;
            public string LogString { get
                {
                    if (log == null)
                        log = string.Join("\r\n", logLines);
                    return log;
                } }

            int logIndent = 0;
            void Log(string line)
            {
                logLines.Add(new string(' ', logIndent * 2) + line);
            }
            public static double DSq(double[] vals, Vector3 v2)
            {
                Vector3 v1 = new Vector3(vals[0], vals[1], vals[2]);
                return Vector3.DistanceSquared(v1, v2);
            }
            public bool IsEqual(double[] vals, Vector3 v2)
            {
                Vector3 v1 = new Vector3(vals[0], vals[1], vals[2]);
                return Vector3.DistanceSquared(v1, v2) < vertexMinDist;
            }
            public delegate void LogDel(string line);

            public List<Face> FacesFromId(string id)
            {
                return this.faces.Where(f => f.id == id).ToList();
            }

            public Vertex AddVertex(Vector3 v)
            {
                Vertex ov;
                AddVertex(v, out ov);
                return ov;
            }

            public bool AddVertex(Vector3 v, out Vertex ov)
            {
                var nodes = kdTree.GetNearestNeighbours(new double[] { v.X, v.Y, v.Z }, 1);
                if (nodes.Length > 0 && IsEqual(nodes[0].Point, v))
                {
                    ov = nodes[0].Value;
                    return false;
                }
                else
                {
                    Log(string.Format("Add Vertex {0}", nextVtxIdx));
                    Vertex nv = new Vertex() { pt = v, idx = nextVtxIdx++, mindist = nodes.Length > 0 ? DSq(nodes[0].Point, v) : -1 };
                    kdTree.Add(new double[] { v.X, v.Y, v.Z }, nv);
                    this.vertices.Add(nv);
                    ov = nv;
                    return true;
                }
            }
            EdgePtr MakeEdge(Vertex v0, Vertex v1, Face f)
            {
                try
                {
                    return new EdgePtr(v0, v1, edgeDict, f, Log);
                }
                catch (Exception e)
                {
                    Log(String.Format("Exception for Edge [{0} {1}]: {2}", v0.idx, v1.idx, e.Message));
                    throw e;
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

                    EdgePtr eptr = MakeEdge(v0, v1, null);
                    elist.Add(eptr);
                }

                Face f = new Face(id, elist);
                foreach (EdgePtr eptr in f.edges)
                {
                    eptr.parentFace = f;
                }
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

            void DoBsp()
            {
                RemoveDuplicateFaces();
                Triangulate();

                BSPTree bSPTree = new BSPTree();
                bSPTree.AddFaces(faces);
                List<BSPFace> bspFacss = bSPTree.GetFaces();
                faces.Clear();
                foreach (BSPFace bf in bspFacss)
                {
                    AddFace(bf.f.id, bf.points);
                }
            }

            public void Fix()
            {
                Log("Fix");
                try
                {
                    RemoveDuplicateFaces();
                    Triangulate();
                    vertexMinDist *= 0.01;
                    //Log("SplitXJunctions");
                    //SplitXJunctions();                    
                    Log("SplitTJunctions");
                    logIndent++;
                    SplitTJunctions();
                    logIndent--;
                    //Log("SplitInteriorEdges");
                    logIndent++;
                    SplitInteriorEdges();
                    logIndent--;
                    Log("SplitIntersectingEdges");
                    logIndent++;
                    SplitIntersectingEdges();
                    logIndent--;
                    Log("RemoveSplitEdgesFromFaces");
                    logIndent++;
                    RemoveSplitEdgesFromFaces();
                    logIndent--;
                    Log("SplitTJunctions");
                    logIndent++;
                    SplitTJunctions();
                    FixWindings();
                    logIndent--;
                    List<Edge> nme = new List<Edge>();
                    GetNonManifold(nme);
                }
                catch (Exception ex)
                {

                }
                Dictionary<ulong, Edge> newDict =
                    new Dictionary<ulong, Edge>(
                        edgeDict.Where(kv => !kv.Value.split));
                edgeDict = newDict;
                int idx = 0;
                foreach (Face f in faces)
                {
                    f.idx = idx++;
                }
                //faces = faces.Where(f => f.visited).ToList();
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
                                }
                            }
                        }
                        foreach (Face f in faces)
                        {
                            if (f.aabb.Contains(e.v0.pt) != AABB.ContainmentType.Disjoint &&
                                f.IsPointInsideTriangle(e.v0) &&
                                !f.ContainsVertex(e.v1.idx))
                            {
                                if (!f.IsPointOnTriangle(e.v1))
                                {
                                    if (SplitFaceOnEdge(f, e))
                                    {
                                        didSplit = true;
                                        break;
                                    }
                                }
                                else
                                {
                                    AddFaceInteriorPoint(f, e.v0);
                                    didSplit = true;
                                    break;
                                }
                            }
                            if (f.aabb.Contains(e.v1.pt) != AABB.ContainmentType.Disjoint &&
                                f.IsPointInsideTriangle(e.v1) &&
                                !f.ContainsVertex(e.v0.idx))
                            {
                                if (!f.IsPointOnTriangle(e.v0))
                                {
                                    if (SplitFaceOnEdge(f, e))
                                    {
                                        didSplit = true;
                                        break;
                                    }
                                }
                                else
                                {
                                    AddFaceInteriorPoint(f, e.v1);
                                    didSplit = true;
                                    break;
                                }
                            }
                        }

                        bool cont = true;
                        foreach (Edge adjE in e.v0.edges)
                        {
                            if (adjE == e)
                                continue;
                            if (e.v0.idx == adjE.v0.idx && Eps.Eq(Vector3.Dot(adjE.dir, e.dir), 1))
                            {
                                if (e.len > adjE.len)
                                {
                                    SplitEdge(e, adjE.v1);
                                }
                                else
                                {
                                    SplitEdge(adjE, e.v1);
                                }
                                cont = false;
                            }
                            else if (e.v0.idx == adjE.v1.idx && Eps.Eq(Vector3.Dot(adjE.dir, e.dir), -1))
                            {
                                if (e.len > adjE.len)
                                {
                                    Debugger.Break();
                                }
                                else
                                {
                                    double dist = adjE.DistanceFromPt(e.v1.pt);
                                    SplitEdge(adjE, e.v1);
                                }
                                cont = false;
                            }
                            if (!cont)
                                break;
                        }
                        if (!cont)
                            break;
                        foreach (Edge adjE in e.v1.edges)
                        {
                            if (adjE == e)
                                continue;
                            if (e.v1.idx == adjE.v1.idx && Eps.Eq(Vector3.Dot(adjE.dir, e.dir), 1))
                            {
                                if (e.len > adjE.len)
                                    Debugger.Break();
                                else
                                {
                                    SplitEdge(adjE, e.v0);
                                }
                                // double dist = e.DistanceFromPt(adjE.v0.pt);
                                cont = false;
                            }
                            else if (e.v1.idx == adjE.v0.idx && Eps.Eq(Vector3.Dot(adjE.dir, e.dir), -1))
                            {
                                //double dist = e.DistanceFromPt(adjE.v1.pt);
                                if (e.len > adjE.len)
                                    SplitEdge(e, adjE.v1);
                                else
                                {
                                    SplitEdge(adjE, e.v0);
                                }
                                cont = false;
                            }
                            if (!cont)
                                break;
                        }
                        if (!cont)
                            break;
                    }
                } while (didSplit);
            }

            void SplitIntersectingEdges()
            {
                bool foundintersections = false;
                do
                {
                    foundintersections = false;
                    List<Intersection> intersections =
                        edgeIntersect.FindAllIntersections(
                            vertices,
                        edgeDict.Values.Where(v => !v.split).ToList(), Log);
                    int cnt = 0;
                    foreach (Intersection i in intersections)
                    {
                        Vertex ivtx;
                        bool isnew = AddVertex(i.pt, out ivtx);
                        cnt++;
                        if (!isnew)
                            continue;

                        foundintersections = true;
                        Edge e1 = i.e1;
                        while (e1.split)
                        {
                            double dppa = Vector3.Dot(e1.dir, ivtx.pt - e1.v0.pt);
                            double dppb = Vector3.Dot(e1.dir, e1.v1.pt - e1.v0.pt);
                            if (dppa < 0 || dppa > dppb)
                                Debugger.Break();
                            Edge es1 = e1.splitEdges[0];
                            Edge es2 = e1.splitEdges[1];

                            double dppc = Vector3.Dot(e1.dir, (es1.v0.idx == e1.v0.idx ? es1.v1.pt : es1.v0.pt) - e1.v0.pt);
                            if (dppa > dppc)
                                e1 = es2;
                            else
                                e1 = es1;
                        }
                        SplitEdge(e1, ivtx);

                        Edge e2 = i.e2;
                        double t = Vector3.Dot(ivtx.pt - e2.v0.pt, e2.dir);
                        double t2 = Vector3.Dot(e2.v1.pt - e2.v0.pt, e2.dir);
                        while (e2.split)
                        {
                            Edge es1 = e2.splitEdges[0];
                            double dp1a = Vector3.Dot(es1.dir, ivtx.pt - es1.v0.pt);
                            double dp1b = Vector3.Dot(es1.dir, es1.v1.pt - es1.v0.pt);
                            Edge es2 = e2.splitEdges[1];
                            double dp2a = Vector3.Dot(es2.dir, ivtx.pt - es2.v0.pt);
                            double dp2b = Vector3.Dot(es2.dir, es2.v1.pt - es2.v0.pt);
                            if (dp1a > 0 && dp1a < dp1b)
                                e2 = es1;
                            else if (dp2a > 0 && dp2a < dp2b)
                                e2 = es2;
                            else
                                Debugger.Break();
                        }

                        SplitEdge(e2, ivtx);
                    }
                } while (false);
            }
            void AddFaceInteriorPoint(Face f, Vertex v)
            {
                Face t1 = new Face(f.id, new List<EdgePtr> { f.edges[0],
                    MakeEdge(f.edges[1].V0, v, null),
                    MakeEdge(v, f.edges[0].V0, null) });
                this.faces.Add(t1);
                Face t2 = new Face(f.id, new List<EdgePtr> { f.edges[1],
                    MakeEdge(f.edges[2].V0, v, null),
                    MakeEdge(v, f.edges[1].V0, null) });
                this.faces.Add(t2);
                Face t3 = new Face(f.id, new List<EdgePtr> { f.edges[2],
                    MakeEdge(f.edges[0].V0, v, null),
                    MakeEdge(v, f.edges[2].V0, null) });
                this.faces.Add(t3);
                this.faces.Remove(f);
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

                EdgePtr e2a = MakeEdge(e1a.V1, e0a.V0, null);
                fa = new Face(f.id, new List<EdgePtr> { e0a, e1a, e2a });
                EdgePtr e0b = f.edges[2];
                EdgePtr e1b = f.edges[3];

                EdgePtr e2b = MakeEdge(e1b.V1, e0b.V0, null);
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
                    nonMFEdges.Sort((a, b) => b.len.CompareTo(a.len));
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
                Vector2 p2, Vector2 p3, out double ot)
            {
                Vector2 s1, s2;
                s1.X = p1.X - p0.X; s1.Y = p1.Y - p0.Y;
                s2.X = p3.X - p2.X; s2.Y = p3.Y - p2.Y;

                double s, t;
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
                    Face f1 = new Face(f.id, new List<EdgePtr> { MakeEdge(v, vtx[i], null),
                        MakeEdge(vtx[i], vtx[(i + 1) % 3], null),
                        MakeEdge(vtx[(i + 1) % 3], v, null) });
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

                double t;
                if (GetLineIntersection(p1, p2, ep0, ep1, out t) && t > 0.01 && t < 0.99)
                {
                    Vector3 newpt = f.edges[0].V0.pt + (f.edges[0].V1.pt - f.edges[0].V0.pt) * t;
                    Vertex nv = AddVertex(newpt);
                    double dist = e.DistanceFromPt(newpt);
                    SplitEdge(f.edges[0].e, nv);
                    if (!e.ContainsVertexIdx(nv.idx))
                        SplitEdge(e, nv);
                    return true;
                }
                if (GetLineIntersection(p2, p3, ep0, ep1, out t) && t > 0.01 && t < 0.99)
                {
                    Vector3 newpt = f.edges[1].V0.pt + (f.edges[1].V1.pt - f.edges[1].V0.pt) * t;
                    Vertex nv = AddVertex(newpt);
                    double dist = e.DistanceFromPt(newpt);
                    SplitEdge(f.edges[1].e, nv);
                    if (!e.ContainsVertexIdx(nv.idx))
                        SplitEdge(e, nv);
                    return true;
                }
                if (GetLineIntersection(p3, p1, ep0, ep1, out t) && t > 0.01 && t < 0.99)
                {
                    Vector3 newpt = f.edges[2].V0.pt + (f.edges[2].V1.pt - f.edges[2].V0.pt) * t;
                    Vertex nv = AddVertex(newpt);
                    double dist = e.DistanceFromPt(newpt);
                    SplitEdge(f.edges[2].e, nv);
                    if (!e.ContainsVertexIdx(nv.idx))
                        SplitEdge(e, nv);
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
                        if (e.split)
                            continue;
                        AABB aabb = AABB.CreateFromPoints(new List<Vector3>() { e.v0.pt, e.v1.pt });
                        double elen = (e.v1.pt - e.v0.pt).Length();


                        foreach (var vtx in vertices)
                        {
                            
                            if (e.v0.idx == 333 &&
                                e.v1.idx == 399 &&
                                vtx.idx == 330
                                )
                                Debugger.Break();
                            if (aabb.ContainsEpsilon(vtx.pt) == AABB.ContainmentType.Disjoint)
                                continue;

                            Vector3 ptdir1 = Vector3.Normalize(vtx.pt - e.v0.pt);
                            Vector3 ptdir2 = Vector3.Normalize(e.v1.pt - vtx.pt);
                            if ((ptdir1 - e.dir).LengthSquared() < Mesh.Epsilon &&
                                (ptdir2 - e.dir).LengthSquared() < Mesh.Epsilon)
                            {
                                double len = (vtx.pt - e.v0.pt).Length();
                                if (len < elen)
                                {
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
                //Debug.WriteLine(string.Format("Split [{0} {1}] {2} l={3}", e.v0.idx, e.v1.idx, vtx.idx,
                //    e.len));
                Log(string.Format("Split Edge [{0} {1}] {2}", e.v0.idx, e.v1.idx, vtx.idx));
                //if (e.v0.idx == 249 && e.v1.idx == 398 && vtx.idx == 421) Debugger.Break();
                List<Face> splitFaces = new List<Face>();
                e.v0.edges.Remove(e);
                e.v1.edges.Remove(e);
                List<EdgePtr> eptrs = new List<EdgePtr>(e.edgePtrs);
                foreach (EdgePtr ce in eptrs)
                {
                    Face f = ce.parentFace;
                    double area = f.Area();
                    bool isvalid = f.IsValid();
                    if (f.ContainsVertex(vtx.idx))
                    {
                        Log(String.Format("Face already contains split vtx [[{0}]] {1}",
                            f, vtx.idx));
                        vtx.errorFlags |= 1;
                        continue;
                    }
                    int epIdx = f.edges.IndexOf(ce);
                    f.edges.RemoveAt(epIdx);
                    EdgePtr e1 = MakeEdge(e.v0, vtx, f);
                    EdgePtr e2 = MakeEdge(vtx, e.v1, f);
                    e.splitEdges = new Edge[]
                    {
                        e1.e,
                        e2.e
                    };
                    f.edges.Add(e1);
                    f.edges.Insert(0, e2);
                    if (Face.CheckDuplicateEdges(f.edges))
                        Debug.WriteLine("Dupe edge");
                    splitFaces.Add(f);
                }
                e.edgePtrs.Clear();
                
                //this.edgeDict.Remove(e.ComputeHash());

                foreach (Face f in splitFaces)
                {
                    f.FixEdgeOrder();
                    Face fa, fb;
                    TriangulateQuad(f, out fa, out fb);
                    this.faces.Remove(f);
                    if (fa.Area() < 0.01f)
                        Log(String.Format("Very small face {0} area = {1}, minedge = {2}", fa, fa.Area(),
                            Math.Min(fa.edges[0].e.len,
                            Math.Min(
                            fa.edges[1].e.len,
                            fa.edges[2].e.len))));

                    if (fb.Area() < 0.01f)
                        Log(String.Format("Very small face {0} area = {1}, minedge = {2}", fa, fb.Area(), 
                            Math.Min(fb.edges[0].e.len,
                            Math.Min(
                            fb.edges[1].e.len,
                            fb.edges[2].e.len))));
                    this.faces.Add(fa);
                    this.faces.Add(fb);
                }
            }

            void RemoveSplitEdgesFromFaces()
            {
                foreach (Face f in faces)
                {
                    foreach (var eptr in f.edges)
                    {
                        if (eptr.e.split)
                        {
                            Log(
                                String.Format("Edge is split {0} {1} --> {2} {3}", eptr.e, eptr.e.len, eptr.e.splitEdges[0], eptr.e.splitEdges[0].len));
                            eptr.e.errorFlags |= 1;
                            //Debugger.Break();
                        }
                    }
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
                    if (!e.split && e.edgePtrs.Count < 2)
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
                    f.isexterior = false;
                }

                Vertex[] maxvtx = new Vertex[6]
                    { vertices[0], vertices[0], vertices[0], vertices[0], vertices[0], vertices[0] };
                foreach (Vertex v in vertices)
                {
                    if (v.pt.X > maxvtx[0].pt.X)
                        maxvtx[0] = v;
                    if (v.pt.Y > maxvtx[1].pt.Y)
                        maxvtx[1] = v;
                    if (v.pt.Z > maxvtx[2].pt.Z)
                        maxvtx[2] = v;
                    if (v.pt.X < maxvtx[3].pt.X)
                        maxvtx[3] = v;
                    if (v.pt.Y < maxvtx[4].pt.Y)
                        maxvtx[4] = v;
                    if (v.pt.Z < maxvtx[5].pt.Z)
                        maxvtx[5] = v;
                }
                foreach (Vertex v in maxvtx)
                {
                    var flist = v.GetFaces();
                    foreach (Face f in flist)
                    {
                        f.visited = false;
                        f.isexterior = false;
                        FixWindingsRecursive(f);
                    }
                }
                
                var interiors = faces.Where(f => f.isexterior).ToList();
                //this.faces = interiors;
            }

            void FixWindingsRecursive(Face f)
            {
                if (f.visited)
                    return;
                f.visited = true;
                f.isexterior = true;
                foreach (var eptr in f.edges)
                {
                    bool isreversed = eptr.reverse;
                    Edge e = eptr.e;
                    if (e.edgePtrs.Count != 2)
                        continue;
                    foreach (var adjacent in e.edgePtrs)
                    {
                        if (adjacent.parentFace == f)
                            continue;
                        if (adjacent.reverse == isreversed)
                        {
                            adjacent.parentFace.ReverseWinding();
                        }
                        FixWindingsRecursive(adjacent.parentFace);
                    }
                }
            }


            bool IsUnique(KdTree<double, Vector3> kd, Vector3 pt)
            {
                var result = kd.GetNearestNeighbours(new double[] { pt.X, pt.Y, pt.Z }, 1);
                if (result.Count() > 0 &&
                    Eps.Eq(result[0].Value, pt))
                {
                    return false;
                }
                kd.Add(new double[] { pt.X, pt.Y, pt.Z }, pt);
                return true;
            }

            Vector3 NearestPt(Vector3 linePt1, Vector3 linePt2, Vector3 testPt)
            {
                Vector3 lineDir = (linePt2 - linePt1);
                double lineDistAlong = Vector3.Dot(testPt - linePt1, lineDir) / lineDir.LengthSquared();
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
                    KdTree<double, Vector3> kd = new KdTree<double, Vector3>(3, new KdTree.Math.DoubleMath());
                    GetBottomEdges(edges);

                    if (edges.Count == 0)
                        return outPts;

                    Vector3 planeNormal = new Vector3(0, 1, 0);
                    double planeDist = Vector3.Dot(edges[0].v0.pt, planeNormal);
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
                            double sinTheta = Vector3.Cross(bisectAngle, v1adir).Length();
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
                            double sinTheta = Vector3.Cross(bisectAngle, v1adir).Length();
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

                        double lenSq = (nearestPt - cpt).LengthSquared();
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
                double maxy = aabb.Max.Y;

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
            public void GetVertices(List<Vtx> vlist, bool allowQuads)
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
                    if (allowQuads && vtxs.Length == 4)
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