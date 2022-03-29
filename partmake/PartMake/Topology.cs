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

        public class Settings
        {
            bool triangulate = false;
            bool addinterioredges = true;
            bool splitxjunctions = false;
            bool splittjunctions = false;
            bool splitintersectingedges = false;
            bool splitinterioredges = false;
            bool removesplitedgesfromfaces = false;
            bool reverseBSPFaces = false;
            bool convexDecomp = false;
            bool bsp = false;

            public bool Triangulate { get => triangulate; set { triangulate = value; SettingsChanged?.Invoke(this, new EventArgs()); } }
            public bool ConvexDecomp { get => convexDecomp; set { convexDecomp = value; SettingsChanged?.Invoke(this, new EventArgs()); } }
            public bool BSP { get => bsp; set { bsp = value; SettingsChanged?.Invoke(this, new EventArgs()); } }

            public bool AddInteriorEdges { get => addinterioredges; set { addinterioredges = value; SettingsChanged?.Invoke(this, new EventArgs()); } }
            public bool SplitXJunctions { get => splitxjunctions; set { splitxjunctions = value; SettingsChanged?.Invoke(this, new EventArgs()); } }
            public bool SplitTJunctions { get => splittjunctions; set { splittjunctions = value; SettingsChanged?.Invoke(this, new EventArgs()); } }
            public bool SplitIntersectingEdges { get => splitintersectingedges; set { splitintersectingedges = value; SettingsChanged?.Invoke(this, new EventArgs()); } }
            public bool SplitInteriorEdges { get => splitinterioredges; set { splitinterioredges = value; SettingsChanged?.Invoke(this, new EventArgs()); } }
            public bool RemoveSplitEdgesFromFaces { get => removesplitedgesfromfaces; set { removesplitedgesfromfaces = value; SettingsChanged?.Invoke(this, new EventArgs()); } }

            public bool ReverseBSPFaces { get => reverseBSPFaces; set { reverseBSPFaces = value; SettingsChanged?.Invoke(this, new EventArgs()); } }

            public event EventHandler SettingsChanged;
        }
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
            public static bool Eq(Vector2 a, Vector2 v)
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

            public double DotAngle
            {
                get
                {
                    if (edgePtrs.Count < 2)
                        return -1;
                    if (edgePtrs[0].parentFace.Plane.idx ==
                        edgePtrs[1].parentFace.Plane.idx)
                        return 1;
                    return Math.Abs(Vector3.Dot(edgePtrs[0].parentFace.Normal,
                        edgePtrs[1].parentFace.Normal));
                }
            }

            public List<Vector3> GetBisectorFace(double size)
            {
                Vector3 n0 = edgePtrs[0].parentFace.Normal;
                Vector3 n1 = edgePtrs[1].parentFace.Normal;
                if (Vector3.Dot(n0, n1) < 0) n1 = -n1;
                Vector3 na = Vector3.Normalize(n0 + n1);
                Vector3 edir = Vector3.Normalize(dir);
                Vector3 bisectNrm = Vector3.Cross(edir, na);
                Vector3 bisectV = Vector3.Cross(bisectNrm, dir);
                return new List<Vector3>()
                {
                    v0.pt + bisectV * size,
                    v1.pt + bisectV * size,
                    v1.pt - bisectV * size,
                    v0.pt - bisectV * size
                };

            }
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

        public class Plane
        {
            public Vector3 normal;
            public double d;
            public Vector3 udir, vdir;
            public int idx;
            public Vector3 Origin => normal * d;
            public double totalArea;

            public override string ToString()
            {
                return $"{idx} N={normal} D={d} TotalArea={totalArea}";
            }
            public Plane(Vector3 _n, double _d, int _i)
            {
                normal = _n;
                d = _d;
                idx = _i;
                totalArea = 0;
                GetRefDirs(out udir, out vdir);
            }

            public void GetRefDirs(out Vector3 xdir, out Vector3 ydir)
            {
                double vx = Math.Abs(normal.X);
                double vy = Math.Abs(normal.Y);
                double vz = Math.Abs(normal.Z);
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

            public bool IsEqual(Vector3 onrm, double od)
            {
                Vector3 diff = normal - onrm;
                bool iseq = Eps.Eq(diff.LengthSquared(), 0);
                return iseq && Math.Abs(od - this.d) < 0.1;
            }

            public double DistFromPt(Vector3 pt)
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

            public uint IsSplit(BSPFace sf)
            {
                int neg = 0, pos = 0, zero = 0;
                foreach (var pt in sf.points)
                {
                    double r = Vector3.Dot(pt - Origin, normal);
                    if (Eps.Eq(r, 0))
                        zero++;
                    else if (r < 0) neg++;
                    else pos++;
                }

                uint splitMask = 0;
                if (neg > 0) splitMask |= 1;
                if (zero > 0) splitMask |= 2;
                if (pos > 0) splitMask |= 4;
                return splitMask;
            }

            public static Vector3 LineIntersection(Vector3 planePoint, Vector3 planeNormal, Vector3 linePoint, Vector3 lineDirection)
            {
                double vd = Vector3.Dot(planeNormal, lineDirection);
                if (Eps.Eq(vd, 0))
                {
                    //System.Diagnostics.Debugger.Break();
                    //return Vector3.Zero;
                }

                double t = (Vector3.Dot(planeNormal, planePoint) - Vector3.Dot(planeNormal, linePoint)) / Vector3.Dot(planeNormal, lineDirection);
                return linePoint + lineDirection * t;
            }

            public void SplitFace(BSPFace sf, out BSPFace negFace, out BSPFace posFace, List<Vector3> splitPts)
            {
                int[] posneg = new int[sf.points.Count()];
                double[] rval = new double[sf.points.Count()];
                int idx = 0;
                foreach (var pt in sf.points)
                {
                    double r = Vector3.Dot(pt - Origin, normal);
                    rval[idx] = r;
                    if (Eps.Eq(r, 0))
                        posneg[idx] = 0;
                    else
                        posneg[idx] = r > 0 ? 1 : -1;
                    idx++;
                }
                List<Vector3> pospts = new List<Vector3>();
                List<Vector3> negpts = new List<Vector3>();
                for (idx = 0; idx < posneg.Length; ++idx)
                {
                    if (posneg[idx] >= 0)
                        pospts.Add(sf.points[idx]);
                    if (posneg[idx] <= 0)
                        negpts.Add(sf.points[idx]);
                    if (posneg[idx] == 0 && splitPts != null)
                        splitPts.Add(sf.points[idx]);

                    int diff = posneg[(idx + 1) % posneg.Length] -
                        posneg[idx];
                    if (diff == 2 || diff == -2)
                    {
                        Vector3 pt0 = sf.points[idx];
                        Vector3 pt1 = sf.points[(idx + 1) % posneg.Length];
                        Vector3 N = normal;
                        Vector3 edgeDir = Vector3.Normalize(pt1 - pt0);
                        double df = Vector3.Dot(edgeDir, sf.Normal);
                        Vector3 ipt = LineIntersection(Origin, normal, pt0, edgeDir);
                        double dp = Vector3.Dot(ipt - Origin, normal);
                        pospts.Add(ipt);
                        negpts.Add(ipt);
                        if (splitPts != null)
                            splitPts.Add(ipt);
                    }
                }

                negFace = (negpts.Count > 2) ? new BSPFace(sf.PlaneDef, sf.f, negpts) : null;
                posFace = (pospts.Count > 2) ? new BSPFace(sf.PlaneDef, sf.f, pospts) : null;
            }
        }

        public class Face : INode
        {
            public List<EdgePtr> edges { get; set; }
            public List<EdgePtr> interioredges { get; set; }
            public bool visited;
            public bool isexterior;
            public bool isinvalid = false;
            public AABB aabb;
            public string id;
            public List<BSPNode> bspNodes { get; set; }
            public List<PortalFace> portalFaces;
            public List<PortalFace> PortalFaces => portalFaces;
            public List<BSPNode> PortalNodes { get => portalFaces?.Select(pf => pf.portal.parentNode).ToList(); }
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
            static double Area(Vector2 d0, Vector2 d1, Vector2 d2)
            {
                double dArea = ((d1.X - d0.X) * (d2.Y - d0.Y) - (d2.X - d0.X) * (d1.Y - d0.Y)) / 2.0f;
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
            public double Area()
            {
                Vector3 nrm = Normal;
                Vector3 xdir = edges[0].Dir;
                Vector3 ydir = Vector3.Cross(xdir, nrm);
                List<Vector2> p = ToFacePts(Vtx.Select(v => v.pt));
                if (p.Count == 3)
                    return Area(p[0], p[1], p[2]);
                else
                {
                    double a1 = Area(p[0], p[1], p[2]);
                    double a2 = Area(p[0], p[2], p[3]);
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
                return !ContainsVertex(vtx.idx) && IsPointOnTriangle(vtx, 0);
            }
            public bool IsPointInsidePoly(Vertex vtx)
            {
                return (!ContainsVertex(vtx.idx) && IsPointOnPoly(vtx));
            }

            public bool IsPointOnPoly(Vertex vtx)
            {
                for (int i = 0; i < edges.Count(); ++i)
                {
                    if (IsPointOnTriangle(vtx, i))
                        return true;
                }
                return false;
            }
            public bool IsPointOnTriangle(Vertex vtx)
            {
                return IsPointOnTriangle(vtx, 0);
            }
            public bool IsPointOnTriangle(Vertex vtx, int trioffset)
            {
                int ct = edges.Count();
                Vector3 pt = vtx.pt;
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
                double alpha = ((p2.Y - p3.Y) * (p.X - p3.X) + (p3.X - p2.X) * (p.Y - p3.Y)) /
                        ((p2.Y - p3.Y) * (p1.X - p3.X) + (p3.X - p2.X) * (p1.Y - p3.Y));
                double beta = ((p3.Y - p1.Y) * (p.X - p3.X) + (p1.X - p3.X) * (p.Y - p3.Y)) /
                       ((p2.Y - p3.Y) * (p1.X - p3.X) + (p3.X - p2.X) * (p1.Y - p3.Y));
                double gamma = 1.0f - alpha - beta;

                return alpha > 0 && beta > 0 && gamma > 0;
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

            public override IEnumerable<INode> Children => edges;
        }

        public class Mesh
        {
            public static Settings settings = null;
            public static double Epsilon = 0.00001f;
            public KdTree<double, Vertex> kdTree = new KdTree<double, Vertex>(3, new KdTree.Math.DoubleMath());
            List<Vertex> vertices = new List<Vertex>();
            public List<Face> faces = new List<Face>();
            public List<ConvexMesh> convexDecomp = new List<ConvexMesh>();
            int nextVtxIdx = 0;
            public Dictionary<ulong, Edge> edgeDict = new Dictionary<ulong, Edge>();
            static double vertexMinDist = 0.0005;
            EdgeIntersectCPP edgeIntersect = new EdgeIntersectCPP();
            public List<string> logLines = new List<string>();
            PlaneMgr planeMgr = new PlaneMgr();

            string log = null;
            public string LogString
            {
                get
                {
                    if (log == null)
                        log = string.Join("\r\n", logLines);
                    return log;
                }
            }

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
            public static bool IsEqual(double[] vals, Vector3 v2)
            {
                Vector3 v1 = new Vector3(vals[0], vals[1], vals[2]);
                return Vector3.DistanceSquared(v1, v2) < vertexMinDist;
            }
            public static bool IsEqual(double[] vals, Vector2 v2)
            {
                Vector2 v1 = new Vector2(vals[0], vals[1]);
                return Vector2.DistanceSquared(v1, v2) < vertexMinDist;
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

            public Face MakeFace(Face origFace, List<Vector3> vertices)
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

                Face f = new Face(origFace.id, elist, planeMgr);
                foreach (EdgePtr eptr in f.edges)
                {
                    eptr.parentFace = f;
                }

                return f;
            }
            public Face MakeFace(string id, List<Vector3> vertices)
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
                    if (v0.idx == v1.idx)
                        continue;

                    EdgePtr eptr = MakeEdge(v0, v1, null);
                    elist.Add(eptr);
                }

                Face f = new Face(id, elist, planeMgr);
                foreach (EdgePtr eptr in f.edges)
                {
                    eptr.parentFace = f;
                }

                return f;
            }

            public Face AddFace(string id, List<Vector3> vertices, int idx = -1)
            {
                Face f = MakeFace(id, vertices);
                if (idx < 0)
                    this.faces.Add(f);
                else
                    this.faces.Insert(idx, f);
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

            public BSPTree bSPTree;
            Random r = new Random();
            void DoBsp(bool reverse)
            {
                bSPTree = new BSPTree();
                List<Face> bspFaces = new List<Face>(faces);
                if (reverse)
                {
                    List<Tuple<double, Face>> randomList = new List<Tuple<double, Face>>();
                    foreach (var face in bspFaces)
                    {
                        randomList.Add(new Tuple<double, Face>(r.NextDouble(), face));
                    }
                    randomList.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                    //bspFaces.Reverse();
                    bspFaces = randomList.Select(r => r.Item2).ToList();
                }


                bSPTree.BuildTree(bspFaces, planeMgr);
                var finalFaces = bSPTree.FindFinalFaces();
                /*
                this.faces.Clear();
                foreach (var face in finalFaces)
                {
                    this.faces.Add(MakeFace("", face));
                }*/
            }


            public void WriteCollision(string filename)
            {
                List<Vector3> vlist = new List<Vector3>();
                GetTrianglePts(vlist);
                Convex c = new Convex();
                c.writeToFile = filename;
                this.convexDecomp = c.Decomp(vlist);
            }
            public void Fix()
            {
                try
                {
                    Log("Fix");
                    this.logLines.Clear();
                    if (settings.BSP)
                    {
                        AddBisectorFaces();
                        int idx = 0;
                        foreach (Face f in faces)
                        {
                            f.idx = idx++;
                        }
                        DoBsp(settings.ReverseBSPFaces);
                    }
                    //RemoveDuplicateFaces();
                    vertexMinDist *= 0.01;
                    if (settings.AddInteriorEdges)
                        AddInteriorEdges();
                    if (settings.Triangulate)
                        Triangulate();
                    if (settings.SplitXJunctions)
                        SplitXJunctions();
                    if (settings.SplitTJunctions)
                        SplitTJunctions();
                    if (settings.SplitIntersectingEdges)
                        SplitIntersectingEdges();
                    if (settings.SplitInteriorEdges)
                        SplitInteriorEdges();
                    if (settings.RemoveSplitEdgesFromFaces)
                        RemoveSplitEdgesFromFaces();
                    //SplitTJunctions();
                    /*
                    FixWindings();
                    List<Edge> nme = new List<Edge>();
                    GetNonManifold(nme);*/
                }
                catch (Exception ex)
                {

                }
                Dictionary<ulong, Edge> newDict =
                    new Dictionary<ulong, Edge>(
                        edgeDict.Where(kv => !kv.Value.split));
                edgeDict = newDict;
                if (settings.ConvexDecomp)
                {
                    List<Vector3> vlist = new List<Vector3>();
                    GetTrianglePts(vlist);
                    Convex c = new Convex();
                    this.convexDecomp = c.Decomp(vlist);
                    foreach (var dcmp in this.convexDecomp)
                    {
                        dcmp.color = BSPPortal.GenColor();
                    }
                    Log($"decomp {this.convexDecomp.Count} parts");
                    Log($"decomp {this.convexDecomp.Select(c => c.points.Count).Sum()} total points");
                }
                //faces = faces.Where(f => f.visited).ToList();
            }

            void AddBisectorFaces()
            {
                List<Edge> bisectEdges = new List<Edge>();
                foreach (var kv in edgeDict)
                {
                    double da = kv.Value.DotAngle;
                    if (da > 0.999 && da < 1)
                        bisectEdges.Add(kv.Value);
                }
                foreach (var edge in bisectEdges)
                {
                    int f0idx = faces.IndexOf(edge.edgePtrs[0].parentFace);
                    int f1idx = faces.IndexOf(edge.edgePtrs[1].parentFace);

                    AddFace("b", edge.GetBisectorFace(0.5), Math.Min(f0idx, f1idx));
                }
            }
            void AddInteriorEdges()
            {
                List<Edge> nonMFEdges = new List<Edge>();
                GetNonManifold(nonMFEdges);
                foreach (Edge e in nonMFEdges)
                {

                    foreach (Face f in faces)
                    {
                        if (f.aabb.Contains(e.v0.pt) != AABB.ContainmentType.Disjoint &&
                            f.aabb.Contains(e.v1.pt) != AABB.ContainmentType.Disjoint &&
                            f.IsPointInsidePoly(e.v0))
                        {
                            if (f.IsPointOnPoly(e.v1))
                            {
                                if (f.interioredges == null) f.interioredges = new List<EdgePtr>();
                                f.interioredges.Add(new EdgePtr(e, false, f));
                            }
                        }
                        else if (f.aabb.Contains(e.v0.pt) != AABB.ContainmentType.Disjoint &&
                            f.aabb.Contains(e.v1.pt) != AABB.ContainmentType.Disjoint &&
                            f.IsPointInsidePoly(e.v1))
                        {
                            if (f.IsPointOnPoly(e.v0))
                            {
                                if (f.interioredges == null) f.interioredges = new List<EdgePtr>();
                                f.interioredges.Add(new EdgePtr(e, false, f));
                            }
                        }
                    }
                }
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
                Log("SplitIntersectingEdges");
                logIndent++;
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
                logIndent--;
            }
            void AddFaceInteriorPoint(Face f, Vertex v)
            {
                Face t1 = new Face(f.id, new List<EdgePtr> { f.edges[0],
                    MakeEdge(f.edges[1].V0, v, null),
                    MakeEdge(v, f.edges[0].V0, null) }, planeMgr);
                this.faces.Add(t1);
                Face t2 = new Face(f.id, new List<EdgePtr> { f.edges[1],
                    MakeEdge(f.edges[2].V0, v, null),
                    MakeEdge(v, f.edges[1].V0, null) }, planeMgr);
                this.faces.Add(t2);
                Face t3 = new Face(f.id, new List<EdgePtr> { f.edges[2],
                    MakeEdge(f.edges[0].V0, v, null),
                    MakeEdge(v, f.edges[2].V0, null) }, planeMgr);
                this.faces.Add(t3);
                this.faces.Remove(f);
            }
            void Triangulate()
            {
                List<Face> newFaces = new List<Face>();
                foreach (Face f in faces)
                {
                    if (f.interioredges != null)
                    {
                        var tris = Triangulator.Face(f);
                        foreach (var tri in tris)
                        {
                            newFaces.Add(this.MakeFace(f, tri));
                        }
                        //set_surfaces.AddRange()
                        Log(String.Format("{0} sides", f.edges.Count()));
                    }
                    else if (f.edges.Count() == 3)
                        newFaces.Add(f);
                    else if (f.edges.Count() == 4)
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
                double d0a = Vector3.Dot(f.edges[0].Dir, f.edges[1].Dir);
                double d1a = Vector3.Dot(f.edges[2].Dir, f.edges[3].Dir);
                double dda = d0a + d1a;

                double d0b = Vector3.Dot(f.edges[1].Dir, f.edges[2].Dir);
                double d1b = Vector3.Dot(f.edges[3].Dir, f.edges[0].Dir);
                double ddb = d0b + d1b;

                EdgePtr e0a = f.edges[0];
                EdgePtr e1a = f.edges[1];

                EdgePtr e2a = MakeEdge(e1a.V1, e0a.V0, null);
                fa = new Face(f.id, new List<EdgePtr> { e0a, e1a, e2a }, planeMgr);
                EdgePtr e0b = f.edges[2];
                EdgePtr e1b = f.edges[3];

                EdgePtr e2b = MakeEdge(e1b.V1, e0b.V0, null);
                fb = new Face(f.id, new List<EdgePtr> { e0b, e1b, e2b }, planeMgr);
            }

            void SplitXJunctions()
            {
                Log("SplitXJunctions");
                logIndent++;
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
                logIndent--;
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
                        MakeEdge(vtx[(i + 1) % 3], v, null) }, planeMgr);
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
                Log("SplitTJunctions");
                logIndent++;

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
                logIndent--;
            }
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
                Log("RemoveSplitEdgesFromFaces");
                logIndent++;
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
                logIndent--;
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


            public List<Tuple<Vector3, Plane>> GetRStuds(Vector3[] candidates, List<Tuple<Vector3, Vector3>> bisectors)
            {
                Dictionary<int, Plane> planes = new Dictionary<int, Plane>();
                foreach (Face f in faces)
                {
                    if (f.Plane.totalArea > 100.0)
                    {
                        if (!planes.ContainsKey(f.Plane.idx))
                            planes.Add(f.Plane.idx, f.Plane);
                    }
                }
                List<Tuple<Vector3, Plane>> outPts = new List<Tuple<Vector3, Plane>>();
                foreach (var kvplane in planes)
                {
                    foreach (var kv in this.edgeDict)
                    {
                        kv.Value.flag = 0;
                    }
                    List<Edge> edges = new List<Edge>();
                    List<Vector3> candidatePts = new List<Vector3>();
                    {
                        KdTree<double, Vector3> kd = new KdTree<double, Vector3>(3, new KdTree.Math.DoubleMath());
                        GetPlaneEdges(kvplane.Value, edges);

                        if (edges.Count == 0)
                            continue;

                        Vector3 planeNormal = kvplane.Value.normal;
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
                        {
                            Debug.WriteLine(kvplane.Key);
                            outPts.Add(new Tuple<Vector3, Plane>(cpt, kvplane.Value));
                        }
                    }
                }
                outPts = outPts.Distinct().ToList();
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
                    if (Eps.Eq(e.v0.pt.Y, maxy) && Eps.Eq(e.v1.pt.Y, maxy))
                        edges.Add(e);
                }
            }

            public void GetPlaneEdges(Plane p, List<Edge> edges)
            {
                foreach (var e in this.edgeDict.Values)
                {
                    if (Eps.Eq(p.DistFromPt(e.v0.pt), 0) &&
                        Eps.Eq(p.DistFromPt(e.v1.pt), 0))
                        edges.Add(e);
                }
            }

            public void GetVertices(List<Vtx> vlist, List<int> faceIndices, bool allowQuads)
            {
                int faceIdx = 0;
                foreach (var f in faces)
                {
                    Vector3 nrm = f.Normal;
                    var vl = f.Vtx;
                    Vtx[] vtxs = vl.Select(v => new Vtx(v.pt, nrm, new Vector2(f.isinvalid ? 1 : 0, 0))).ToArray();
                    vlist.Add(vtxs[0]);
                    vlist.Add(vtxs[1]);
                    vlist.Add(vtxs[2]);
                    faceIndices.Add(faceIdx);
                    if (allowQuads && vtxs.Length == 4)
                    {
                        vlist.Add(vtxs[0]);
                        vlist.Add(vtxs[2]);
                        vlist.Add(vtxs[3]);
                        faceIndices.Add(faceIdx);
                    }
                    faceIdx++;
                }
            }

            void GetTrianglePts(List<Vector3> vlist)
            {
                foreach (var f in faces)
                {
                    var vl = f.Vtx;
                    Vector3[] vtxs = vl.Select(v => v.pt).ToArray();
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

        public class PlaneMgr
        {
            KdTree<double, Plane> kdTreePlane =
                new KdTree<double, Plane>(4, new KdTree.Math.DoubleMath());
            KdTree<double, Vector3> kdTreePoint = new KdTree<double, Vector3>(3, new KdTree.Math.DoubleMath());
            int planeIdx = 0;
            int nextVtxIdx = 0;
            public Plane GetPlane(Vector3 normal, double d)
            {
                if (d < 0)
                { d = -d; normal = -normal; }

                var nodes = kdTreePlane.GetNearestNeighbours(new double[] { normal.X, normal.Y, normal.Z,
                        d }, 1);

                if (nodes.Length > 0 && nodes[0].Value.IsEqual(normal, d))
                {
                    return nodes[0].Value;
                }
                else
                {
                    Plane def = new Plane(normal, d, planeIdx++);
                    kdTreePlane.Add(new double[] { def.normal.X, def.normal.Y, def.normal.Z,
                            def.d }, def);
                    return def;
                }

            }

            public Vector3 AddPoint(double x, double y, double z)
            {
                var nodes = kdTreePoint.GetNearestNeighbours(new double[] { x, y, z }, 1);
                if (nodes.Length > 0 && Mesh.IsEqual(nodes[0].Point, new Vector3(x, y, z)))
                {
                    return nodes[0].Value;
                }
                else
                {
                    Vector3 nv = new Vector3(x, y, z);
                    kdTreePoint.Add(new double[] { x, y, z }, nv);
                    nextVtxIdx++;
                    return nv;
                }
            }
        }

    }
}