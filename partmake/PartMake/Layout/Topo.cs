using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Diagnostics;
using KdTree;

namespace partmake.Layout.Topo
{
    public class Vertex
    {
        public int idx;
        public Vector3 pt;
        public List<Edge> edges =
            new List<Edge>();
        public int errorFlags = 0;
        public float mindist = -1;

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
    /// <summary>
    /// Provides XNA-like axis-aligned bounding box functionality.
    /// </summary>
    public struct AABB
    {
        /// <summary>
        /// Location with the lowest X, Y, and Z coordinates in the axis-aligned bounding box.
        /// </summary>
        public Vector3 Min;

        /// <summary>
        /// Location with the highest X, Y, and Z coordinates in the axis-aligned bounding box.
        /// </summary>
        public Vector3 Max;

        /// <summary>
        /// Constructs a bounding box from the specified minimum and maximum.
        /// </summary>
        /// <param name="min">Location with the lowest X, Y, and Z coordinates contained by the axis-aligned bounding box.</param>
        /// <param name="max">Location with the highest X, Y, and Z coordinates contained by the axis-aligned bounding box.</param>
        public AABB(Vector3 min, Vector3 max)
        {
            this.Min = min;
            this.Max = max;
        }

        public void Grow(float size)
        {
            this.Min -= new Vector3(size, size, size);
            this.Max += new Vector3(size, size, size);
        }

        /// <summary>
        /// Gets an array of locations corresponding to the 8 corners of the bounding box.
        /// </summary>
        /// <returns>Corners of the bounding box.</returns>
        public Vector3[] GetCorners()
        {
            var toReturn = new Vector3[8];
            toReturn[0] = new Vector3(Min.X, Max.Y, Max.Z);
            toReturn[1] = Max;
            toReturn[2] = new Vector3(Max.X, Min.Y, Max.Z);
            toReturn[3] = new Vector3(Min.X, Min.Y, Max.Z);
            toReturn[4] = new Vector3(Min.X, Max.Y, Min.Z);
            toReturn[5] = new Vector3(Max.X, Max.Y, Min.Z);
            toReturn[6] = new Vector3(Max.X, Min.Y, Min.Z);
            toReturn[7] = Min;
            return toReturn;
        }


        /// <summary>
        /// Determines if a bounding box intersects another bounding box.
        /// </summary>
        /// <param name="boundingBox">Bounding box to test against.</param>
        /// <returns>Whether the bounding boxes intersected.</returns>
        public bool Intersects(AABB boundingBox)
        {
            if (boundingBox.Min.X > Max.X || boundingBox.Min.Y > Max.Y || boundingBox.Min.Z > Max.Z)
                return false;
            if (Min.X > boundingBox.Max.X || Min.Y > boundingBox.Max.Y || Min.Z > boundingBox.Max.Z)
                return false;
            return true;

        }

        /// <summary>
        /// Determines if a bounding box intersects another bounding box.
        /// </summary>
        /// <param name="boundingBox">Bounding box to test against.</param>
        /// <param name="intersects">Whether the bounding boxes intersect.</param>
        public void Intersects(ref AABB boundingBox, out bool intersects)
        {
            if (boundingBox.Min.X > Max.X || boundingBox.Min.Y > Max.Y || boundingBox.Min.Z > Max.Z)
            {
                intersects = false;
                return;
            }
            if (Min.X > boundingBox.Max.X || Min.Y > boundingBox.Max.Y || Min.Z > boundingBox.Max.Z)
            {
                intersects = false;
                return;
            }
            intersects = true;
        }


        public enum ContainmentType
        {
            Disjoint,
            Contains,
            Intersects
        }
        //public bool Intersects(BoundingFrustum frustum)
        //{
        //    bool intersects;
        //    frustum.Intersects(ref this, out intersects);
        //    return intersects;
        //}

        public ContainmentType Contains(ref AABB boundingBox)
        {
            if (Max.X < boundingBox.Min.X || Min.X > boundingBox.Max.X ||
                Max.Y < boundingBox.Min.Y || Min.Y > boundingBox.Max.Y ||
                Max.Z < boundingBox.Min.Z || Min.Z > boundingBox.Max.Z)
                return ContainmentType.Disjoint;
            //It is known to be at least intersecting. Is it contained?
            if (Min.X <= boundingBox.Min.X && Max.X >= boundingBox.Max.X &&
                Min.Y <= boundingBox.Min.Y && Max.Y >= boundingBox.Max.Y &&
                Min.Z <= boundingBox.Min.Z && Max.Z >= boundingBox.Max.Z)
                return ContainmentType.Contains;
            return ContainmentType.Intersects;
        }


        public ContainmentType ContainsEpsilon(Vector3 v)
        {
            if (v.X < (Min.X - Eps.Epsilon) || v.X > (Max.X + Eps.Epsilon))
                return ContainmentType.Disjoint;
            if (v.Y < (Min.Y - Eps.Epsilon) || v.Y > (Max.Y + Eps.Epsilon))
                return ContainmentType.Disjoint;
            if (v.Z < (Min.Z - Eps.Epsilon) || v.Z > (Max.Z + Eps.Epsilon))
                return ContainmentType.Disjoint;
            return ContainmentType.Intersects;
        }

        public ContainmentType Contains(Vector3 v)
        {
            if (v.X < Min.X || v.X > Max.X)
                return ContainmentType.Disjoint;
            if (v.Y < Min.Y || v.Y > Max.Y)
                return ContainmentType.Disjoint;
            if (v.Z < Min.Z || v.Z > Max.Z)
                return ContainmentType.Disjoint;
            return ContainmentType.Intersects;
        }


        /// <summary>
        /// Creates the smallest possible bounding box that contains a list of points.
        /// </summary>
        /// <param name="points">Points to enclose with a bounding box.</param>
        /// <returns>Bounding box which contains the list of points.</returns>
        public static AABB CreateFromPoints(IEnumerable<Vector3> points)
        {
            AABB aabb;
            var ee = points.GetEnumerator();
            bool cont = ee.MoveNext();
            aabb.Min = ee.Current;
            aabb.Max = aabb.Min;
            while (ee.MoveNext())
            {
                Vector3 v = ee.Current;
                if (v.X < aabb.Min.X)
                    aabb.Min.X = v.X;
                else if (v.X > aabb.Max.X)
                    aabb.Max.X = v.X;

                if (v.Y < aabb.Min.Y)
                    aabb.Min.Y = v.Y;
                else if (v.Y > aabb.Max.Y)
                    aabb.Max.Y = v.Y;

                if (v.Z < aabb.Min.Z)
                    aabb.Min.Z = v.Z;
                else if (v.Z > aabb.Max.Z)
                    aabb.Max.Z = v.Z;
            }
            return aabb;
        }

        /// <summary>
        /// Creates the smallest bounding box which contains two other bounding boxes.
        /// </summary>
        /// <param name="a">First bounding box to be contained.</param>
        /// <param name="b">Second bounding box to be contained.</param>
        /// <param name="merged">Smallest bounding box which contains the two input bounding boxes.</param>
        public static void CreateMerged(ref AABB a, ref AABB b, out AABB merged)
        {
            if (a.Min.X < b.Min.X)
                merged.Min.X = a.Min.X;
            else
                merged.Min.X = b.Min.X;
            if (a.Min.Y < b.Min.Y)
                merged.Min.Y = a.Min.Y;
            else
                merged.Min.Y = b.Min.Y;
            if (a.Min.Z < b.Min.Z)
                merged.Min.Z = a.Min.Z;
            else
                merged.Min.Z = b.Min.Z;

            if (a.Max.X > b.Max.X)
                merged.Max.X = a.Max.X;
            else
                merged.Max.X = b.Max.X;
            if (a.Max.Y > b.Max.Y)
                merged.Max.Y = a.Max.Y;
            else
                merged.Max.Y = b.Max.Y;
            if (a.Max.Z > b.Max.Z)
                merged.Max.Z = a.Max.Z;
            else
                merged.Max.Z = b.Max.Z;
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

        public Vector3 Interp(float t)
        {
            return v0.pt + (v1.pt - v0.pt) * t;
        }
        public Vertex v0 { get; set; }
        public Vertex v1 { get; set; }
        public Vector3 dir { get; set; }

        public IEnumerable<Face> Faces => edgePtrs.Select(eptr => eptr.parentFace);
        public float len { get; set; }
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

        public override string ToString()
        {
            return String.Format("Edge [{0} {1}]", v0.idx, v1.idx);
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
                edgeDict.Add(hash, e);
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

    public class Face
    {
        public List<EdgePtr> edges { get; set; }
        public List<EdgePtr> interioredges { get; set; }
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
            float d1 = Vector3.Dot(edges[0].Dir, edges[1].Dir);
            float d2 = Vector3.Dot(edges[1].Dir, edges[2].Dir);
            float d3 = Vector3.Dot(edges[2].Dir, edges[0].Dir);
            if (Eps.Eq(d1, 1) || Eps.Eq(d1, -1) ||
                Eps.Eq(d2, 1) || Eps.Eq(d2, -1) ||
                Eps.Eq(d3, 1) || Eps.Eq(d3, -1))
                return false;
            return true;
        }

        public Plane Plane { get; }

        static public List<List<Vector3>> GetTriangles(List<Vector3> points)
        {
            List<List<Vector3>> tris = new List<List<Vector3>>();
            if (points.Count == 3)
            {
                tris.Add(points);
            }
            else if (points.Count > 3)
            {
                for (int i = 0; i < points.Count - 2; ++i)
                {
                    tris.Add(new List<Vector3>() { points[0], points[i + 1], points[i + 2] });
                }
            }
            return tris;
        }
        static float Area(Vector2 d0, Vector2 d1, Vector2 d2)
        {
            float dArea = ((d1.X - d0.X) * (d2.Y - d0.Y) - (d2.X - d0.X) * (d1.Y - d0.Y)) / 2.0f;
            return (dArea > 0.0) ? dArea : -dArea;
        }

        public List<Vector2> ToFacePts(IEnumerable<Vector3> pts)
        {
            Vector3 nrm = Normal;
            Vector3 xdir = edges[0].Dir;
            Vector3 ydir = Vector3.Cross(xdir, nrm);
            List<Vector2> v2 = new List<Vector2>();
            Vector3 o = edges[0].V0.pt;
            foreach (Vector3 pt in pts)
            {
                v2.Add(new Vector2(Vector3.Dot((pt - o), xdir),
                    Vector3.Dot((pt - o), ydir)));
            }
            return v2;
        }
        public List<Vector3> ToMeshPts(IEnumerable<Vector2> pts)
        {
            Vector3 nrm = Normal;
            Vector3 xdir = edges[0].Dir;
            Vector3 ydir = Vector3.Cross(xdir, nrm);
            List<Vector3> v3 = new List<Vector3>();
            Vector3 o = edges[0].V0.pt;
            foreach (Vector2 pt in pts)
            {
                v3.Add(o + pt.X * xdir + pt.Y * ydir);
            }
            return v3;
        }
        public Vector3 ToMeshPt(Vector2 pt)
        {
            Vector3 nrm = Normal;
            Vector3 xdir = edges[0].Dir;
            Vector3 ydir = Vector3.Cross(xdir, nrm);
            List<Vector3> v3 = new List<Vector3>();
            Vector3 o = edges[0].V0.pt;
            return o + pt.X * xdir + pt.Y * ydir;
        }
        public float Area()
        {
            Vector3 nrm = Normal;
            Vector3 xdir = edges[0].Dir;
            Vector3 ydir = Vector3.Cross(xdir, nrm);
            List<Vector2> p = ToFacePts(Vtx.Select(v => v.pt));
            if (p.Count == 3)
                return Area(p[0], p[1], p[2]);
            else
            {
                float a1 = Area(p[0], p[1], p[2]);
                float a2 = Area(p[0], p[2], p[3]);
                return a1 + a2;
            }
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
        public Face(string _id, List<EdgePtr> _edges, PlaneMgr mgr)
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
            Plane = mgr.GetPlane(Normal,
                        Vector3.Dot(edges[0].V0.pt, Normal));
            Plane.totalArea += Area();
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
            return !ContainsVertex(vtx.idx) && IsPointOnTriangle(vtx.pt, 0);
        }
        public bool IsPointInsidePoly(Vertex vtx)
        {
            return (!ContainsVertex(vtx.idx) && IsPointOnPoly(vtx.pt));
        }

        public bool IsPointOnPoly(Vector3 pt)
        {
            for (int i = 0; i < edges.Count(); ++i)
            {
                if (IsPointOnTriangle(pt, i))
                    return true;
            }
            return false;
        }
        public bool IsPointOnTriangle(Vector3 pt)
        {
            return IsPointOnTriangle(pt, 0);
        }
        public bool IsPointOnTriangle(Vector3 pt, int trioffset)
        {
            int ct = edges.Count();
            Vector3 nrm = Normal;
            if (!Eps.Eq(Vector3.Dot(pt - edges[(0 + trioffset) % ct].V0.pt, nrm), 0))
                return false;
            Vector3 xdir = edges[0].Dir;
            Vector3 ydir = Vector3.Cross(xdir, nrm);

            Vector2 p1 = new Vector2(Vector3.Dot(edges[(0 + trioffset) % ct].V0.pt, xdir),
                Vector3.Dot(edges[(0 + trioffset) % ct].V0.pt, ydir));
            Vector2 p2 = new Vector2(Vector3.Dot(edges[(1 + trioffset) % ct].V0.pt, xdir),
                Vector3.Dot(edges[(1 + trioffset) % ct].V0.pt, ydir));
            Vector2 p3 = new Vector2(Vector3.Dot(edges[(2 + trioffset) % ct].V0.pt, xdir),
                Vector3.Dot(edges[(2 + trioffset) % ct].V0.pt, ydir));
            Vector2 p = new Vector2(Vector3.Dot(pt, xdir), Vector3.Dot(pt, ydir));
            float alpha = ((p2.Y - p3.Y) * (p.X - p3.X) + (p3.X - p2.X) * (p.Y - p3.Y)) /
                    ((p2.Y - p3.Y) * (p1.X - p3.X) + (p3.X - p2.X) * (p1.Y - p3.Y));
            float beta = ((p3.Y - p1.Y) * (p.X - p3.X) + (p1.X - p3.X) * (p.Y - p3.Y)) /
                   ((p2.Y - p3.Y) * (p1.X - p3.X) + (p3.X - p2.X) * (p1.Y - p3.Y));
            float gamma = 1.0f - alpha - beta;

            return alpha >= 0 && beta >= 0 && gamma >= 0;
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
            var vlist = Vtx.ToList();
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
    }

    public class PlaneMgr
    {
        KdTree<float, Plane> kdTreePlane =
            new KdTree<float, Plane>(4, new KdTree.Math.FloatMath());
        KdTree<float, Vector3> kdTreePoint = new KdTree<float, Vector3>(3, new KdTree.Math.FloatMath());
        int planeIdx = 0;
        int nextVtxIdx = 0;
        public Plane GetPlane(Vector3 normal, float d)
        {
            if (d < 0)
            { d = -d; normal = -normal; }

            var nodes = kdTreePlane.GetNearestNeighbours(new float[] { normal.X, normal.Y, normal.Z,
                        d }, 1);

            if (nodes.Length > 0 && nodes[0].Value.IsEqual(normal, d))
            {
                return nodes[0].Value;
            }
            else
            {
                Plane def = new Plane(normal, d, planeIdx++);
                kdTreePlane.Add(new float[] { def.normal.X, def.normal.Y, def.normal.Z,
                            def.d }, def);
                return def;
            }

        }

        public Vector3 AddPoint(float x, float y, float z)
        {
            var nodes = kdTreePoint.GetNearestNeighbours(new float[] { x, y, z }, 1);
            if (nodes.Length > 0 && IsEqual(nodes[0].Point, new Vector3(x, y, z)))
            {
                return nodes[0].Value;
            }
            else
            {
                Vector3 nv = new Vector3(x, y, z);
                kdTreePoint.Add(new float[] { x, y, z }, nv);
                nextVtxIdx++;
                return nv;
            }
        }

        public static bool IsEqual(float[] vals, Vector3 v2)
        {
            Vector3 v1 = new Vector3(vals[0], vals[1], vals[2]);
            return Vector3.DistanceSquared(v1, v2) < VertexMinDist;
        }

        public static float VertexMinDist = 0.0005f;
    }

    public class Plane
    {
        public Vector3 normal;
        public float d;
        public Vector3 udir, vdir;
        public int idx;
        public Vector3 Origin => normal * d;
        public float totalArea;

        public override string ToString()
        {
            return $"{idx} N={normal} D={d} TotalArea={totalArea}";
        }
        public Plane(Vector3 _n, float _d, int _i)
        {
            normal = _n;
            d = _d;
            idx = _i;
            totalArea = 0;
            GetRefDirs(out udir, out vdir);
        }

        public void GetRefDirs(out Vector3 xdir, out Vector3 ydir)
        {
            float vx = Math.Abs(normal.X);
            float vy = Math.Abs(normal.Y);
            float vz = Math.Abs(normal.Z);
            if (vx > vy && vx > vz)
            {
                // x dominant
                xdir = Vector3.Cross(Vector3.UnitY, normal);
                ydir = Vector3.Cross(xdir, normal);
            }
            else if (vy > vz)
            {
                // y dominant
                xdir = Vector3.Cross(Vector3.UnitX, normal);
                ydir = Vector3.Cross(xdir, normal);
            }
            else
            {
                // z dominant
                xdir = Vector3.Cross(Vector3.UnitY, normal);
                ydir = Vector3.Cross(xdir, normal);
            }

            xdir = Vector3.Normalize(xdir);
            ydir = Vector3.Normalize(ydir);
        }

        public bool IsEqual(Vector3 onrm, float od)
        {
            Vector3 diff = normal - onrm;
            bool iseq = Eps.Eq(diff.LengthSquared(), 0);
            return iseq && Math.Abs(od - this.d) < 0.1;
        }

        public float DistFromPt(Vector3 pt)
        {
            return Vector3.Dot(pt, normal) - d;
        }

        public List<Vector2> ToPlanePts(IEnumerable<Vector3> pts)
        {
            Vector3 nrm = normal;
            List<Vector2> v2 = new List<Vector2>();
            Vector3 o = Origin;
            foreach (Vector3 pt in pts)
            {
                v2.Add(new Vector2(Vector3.Dot((pt - o), udir),
                    Vector3.Dot((pt - o), vdir)));
            }
            return v2;
        }
        public List<Vector3> ToMeshPts(IEnumerable<Vector2> pts)
        {
            Vector3 nrm = normal;
            List<Vector3> v3 = new List<Vector3>();
            Vector3 o = Origin;
            foreach (Vector2 pt in pts)
            {
                v3.Add(o + pt.X * udir + pt.Y * vdir);
            }
            return v3;
        }    

        public static Vector3 LineIntersection(Vector3 planePoint, Vector3 planeNormal, Vector3 linePoint, Vector3 lineDirection)
        {
            float vd = Vector3.Dot(planeNormal, lineDirection);
            if (Eps.Eq(vd, 0))
            {
                //System.Diagnostics.Debugger.Break();
                //return Vector3.Zero;
            }

            float t = (Vector3.Dot(planeNormal, planePoint) - Vector3.Dot(planeNormal, linePoint)) / Vector3.Dot(planeNormal, lineDirection);
            return linePoint + lineDirection * t;
        }
    }

    public class Park
    {
    }
}