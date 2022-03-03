using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.DoubleNumerics;
using System.ComponentModel;
using KdTree;

namespace partmake
{
    namespace Topology
    {

        public class BSPPlaneDef
        {
            public Vector3 normal;
            public double d;
            public Vector3 udir, vdir;
            public int idx;
            public Vector3 Origin => normal * d;

            public BSPPlaneDef(Vector3 _n, double _d, int _i)
            {
                normal = _n;   
                d = _d;                
                idx = _i;
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
                Vector4 diff = new Vector4(normal - onrm, d - od);
                return Eps.Eq(diff.LengthSquared(), 0);
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
                    if (Eps.Eq(r * 0.01, 0))
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
                if (Eps.Eq(Vector3.Dot(planeNormal, lineDirection), 0))
                {
                    // System.Diagnostics.Debugger.Break();
                    return Vector3.Zero;
                }

                double t = (Vector3.Dot(planeNormal, planePoint) - Vector3.Dot(planeNormal, linePoint)) / Vector3.Dot(planeNormal, lineDirection);
                return linePoint + lineDirection * t;
            }

            public void SplitFace(BSPFace sf, out BSPFace negFace, out BSPFace posFace, List<Vector3> splitPts)
            {
                int[] posneg = new int[sf.points.Count()];
                int idx = 0;
                foreach (var pt in sf.points)
                {
                    double r = Vector3.Dot(pt - Origin, normal);
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

                        Vector3 ipt = LineIntersection(Origin, normal, pt0, edgeDir);
                        double dp = Vector3.Dot(ipt - Origin, normal);
                        pospts.Add(ipt);
                        negpts.Add(ipt);
                        if (splitPts != null)
                            splitPts.Add(ipt);
                    }
                }

                negFace = (negpts.Count > 2) ? new BSPFace(sf.PlaneDef, negpts) : null;
                posFace = (pospts.Count > 2) ? new BSPFace(sf.PlaneDef, pospts) : null;
            }
        }
        public class BSPPlane
        {
            BSPPlaneDef def;
            public Vector3 Normal { get => def.normal; }
            public double D => def.d;
            Vector3 udir => def.udir;
            Vector3 vdir => def.vdir;
            public BSPNode parentNode;
            public List<PortalFace> portalFaces;

            public BSPPlaneDef Def => def;

            public BSPPlane(BSPPlaneDef pd, BSPNode p)
            {
                parentNode = p;
                def = pd;
            }

            public uint IsSplit(BSPFace sf)
            { return def.IsSplit(sf); }

            public void SplitFace(BSPFace sf, out BSPFace negFace, out BSPFace posFace, List<Vector3> splitPts)
            {  def.SplitFace(sf, out negFace, out posFace, splitPts); }

            public List<Vector2> ToPlanePts(IEnumerable<Vector3> pts)
            {  return def.ToPlanePts(pts); }
            public List<Vector3> ToMeshPts(IEnumerable<Vector2> pts)
            { return def.ToMeshPts(pts); }
        }
        public class BSPFace
        {
            public Face f;
            public List<Vector3> points;
            BSPPlaneDef planedef;
            public BSPPlaneDef PlaneDef
            {
                get => planedef;
            }            
            //public IEnumerable<BSPNode> ConnectedNodes =>
                //connectedFaces?.Select(f => f.)
            public override string ToString()
            {
                return Normal.ToString();
            }

            public Vector3 Normal => PlaneDef.normal;
            public BSPFace(BSPPlaneDef _p, Face _f)
            {
                planedef = _p;
                f = _f;
                points = f.Vtx.Select(v => v.pt).ToList();
            }

            public BSPFace(PlaneMgr mgr, List<Vector3> _pts)
            {
                Vector3 nrm = Vector3.Normalize(Vector3.Cross((_pts[1] - _pts[0]),
                        (_pts[2] - _pts[1])));
                double d = Vector3.Dot(_pts[0], nrm);
                planedef = mgr.GetPlane(nrm, d);
                points = _pts;
            }
            public BSPFace(BSPPlaneDef _p, List<Vector3> _pts)
            {
                if (_pts.Count < 3)
                    Debugger.Break();
                points = _pts;
                planedef = _p;
            }

            public bool IsValid()
            {
                for (int i = 0; i < points.Count; i++)
                {
                    for (int j = i + 1; j < points.Count; j++)
                    {
                        if (Eps.Eq(points[i], points[j]))
                            return false;
                    }
                }

                return true;
            }

            public List<List<Vector3>> GetTriangles()
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
        }

        public class PortalFace : BSPFace
        {
            public BSPPlane Plane;
            public BSPPortal portal;
            public List<PortalFace> connectedFaces { get; set; }
            public List<BSPNode> ConnectedNodes { get
                {
                    return connectedFaces?.Select(p => p.portal.parentNode).ToList();
                } }
            public PortalFace(BSPPlane pl, List<Vector3> _pts) : base(pl.Def, _pts)
            {
                Plane = pl;
            }

            public PortalFace(PlaneMgr mgr, List<Vector3> _pts) : base(mgr, _pts)
            {
                Plane = new BSPPlane(base.PlaneDef, null);
            }

            public static PortalFace FromPts(BSPPlane pl, BSPPortal pr, List<Vector3> inPts)
            {
                List<Vector2> pts = pl.ToPlanePts(inPts);
                for (int i = 0; i < pts.Count; ++i)
                {
                    for (int j = i + 1; j < pts.Count;)
                    {
                        if (Eps.Eq(pts[i], pts[j]))
                            pts.RemoveAt(j);
                        else
                            ++j;
                    }
                }

                if (pts.Count < 3)
                    return null;

                Vector2 center = new Vector2(0, 0);
                foreach (var pt in pts)
                {
                    center += pt;
                }
                center /= pts.Count();
                List<Vector3> spts = new List<Vector3>();
                foreach (var pt in pts)
                {
                    spts.Add(new Vector3(pt.X, pt.Y, Math.Atan2(pt.X - center.X, pt.Y - center.Y)));
                }
                spts.Sort((a, b) => a.Z.CompareTo(b.Z));

                List<Vector3> meshPts = pl.ToMeshPts(spts.Select(v => new Vector2(v.X, v.Y)));
                return new PortalFace(pl, meshPts);
            }
        }

        public class BSPEdge
        {
            public Vector3 v1;
            public Vector3 v2;
        }
        public class BSPPortal
        {
            public List<PortalFace> Faces => faces;
            List<PortalFace> faces;
            public BSPNode parentNode;

            public BSPPortal(AABB aabb, PlaneMgr mgr)
            {
                faces = new List<PortalFace>()
                {
                    new PortalFace(mgr, new List<Vector3>()
                    {
                        new Vector3(aabb.Min.X, aabb.Min.Y, aabb.Min.Z),
                        new Vector3(aabb.Min.X, aabb.Min.Y, aabb.Max.Z),
                        new Vector3(aabb.Min.X, aabb.Max.Y, aabb.Max.Z),
                        new Vector3(aabb.Min.X, aabb.Max.Y, aabb.Min.Z)
                    }),
                    new PortalFace(mgr, new List<Vector3>()
                    {
                        new Vector3(aabb.Max.X, aabb.Min.Y, aabb.Min.Z),
                        new Vector3(aabb.Max.X, aabb.Min.Y, aabb.Max.Z),
                        new Vector3(aabb.Max.X, aabb.Max.Y, aabb.Max.Z),
                        new Vector3(aabb.Max.X, aabb.Max.Y, aabb.Min.Z)
                    }),
                    new PortalFace(mgr, new List<Vector3>()
                    {
                        new Vector3(aabb.Min.X, aabb.Min.Y, aabb.Min.Z),
                        new Vector3(aabb.Min.X, aabb.Min.Y, aabb.Max.Z),
                        new Vector3(aabb.Max.X, aabb.Min.Y, aabb.Max.Z),
                        new Vector3(aabb.Max.X, aabb.Min.Y, aabb.Min.Z)
                    }),
                    new PortalFace(mgr, new List<Vector3>()
                    {
                        new Vector3(aabb.Min.X, aabb.Max.Y, aabb.Min.Z),
                        new Vector3(aabb.Min.X, aabb.Max.Y, aabb.Max.Z),
                        new Vector3(aabb.Max.X, aabb.Max.Y, aabb.Max.Z),
                        new Vector3(aabb.Max.X, aabb.Max.Y, aabb.Min.Z)
                    }),
                    new PortalFace(mgr, new List<Vector3>()
                    {
                        new Vector3(aabb.Min.X, aabb.Min.Y, aabb.Min.Z),
                        new Vector3(aabb.Min.X, aabb.Max.Y, aabb.Min.Z),
                        new Vector3(aabb.Max.X, aabb.Max.Y, aabb.Min.Z),
                        new Vector3(aabb.Max.X, aabb.Min.Y, aabb.Min.Z)
                    }),
                    new PortalFace(mgr, new List<Vector3>()
                    {
                        new Vector3(aabb.Min.X, aabb.Min.Y, aabb.Max.Z),
                        new Vector3(aabb.Min.X, aabb.Max.Y, aabb.Max.Z),
                        new Vector3(aabb.Max.X, aabb.Max.Y, aabb.Max.Z),
                        new Vector3(aabb.Max.X, aabb.Min.Y, aabb.Max.Z)
                    })
                };
            }

            public BSPPortal(List<PortalFace> _faces)
            {
                faces = _faces.Select(p => new PortalFace(p.Plane, p.points)).ToList();
                foreach (var f in faces)
                {
                    f.portal = this;
                }
            }

            public void SplitFace(BSPPlane plane, out BSPPortal negportal, out BSPPortal posportal)
            {
                List<PortalFace> negFaces = new List<PortalFace>();
                List<PortalFace> posFaces = new List<PortalFace>();
                List<Vector3> planePts = new List<Vector3>();
                foreach (PortalFace f in faces)
                {
                    uint s = plane.IsSplit(f);
                    if (s == 1)
                        negFaces.Add(f);
                    else if (s == 4)
                        posFaces.Add(f);
                    else if (s == 2)
                    {
                        negFaces.Add(f);
                        posFaces.Add(f);
                    }
                    else
                    {
                        BSPFace p, n;
                        plane.SplitFace(f, out n, out p, planePts);
                        if ((s & 1) != 0 && n != null)
                            negFaces.Add(new PortalFace(f.Plane, n.points));
                        if ((s & 4) != 0 && p != null)
                            posFaces.Add(new PortalFace(f.Plane, n.points));
                    }
                }

                if (planePts.Count > 2)
                {
                    PortalFace splitFace = PortalFace.FromPts(plane, this, planePts);
                    if (splitFace != null)
                    {
                        negFaces.Add(splitFace);
                        posFaces.Add(splitFace);
                    }
                }
                negportal = new BSPPortal(negFaces);
                posportal = new BSPPortal(posFaces);
            }
        }

        public class BSPNode : INotifyPropertyChanged
        {
            bool isExpanded = false;
            public bool IsExpanded
            {
                get => isExpanded; set
                {
                    isExpanded = value;
                    if (isExpanded == false && neg != null) neg.IsExpanded = false;
                    if (isExpanded == false && pos != null) pos.IsExpanded = false;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsExpanded"));
                }
            }

            bool isSelected = false;
            public bool IsSelected { get => isSelected; set { isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsSelected")); } }
            public List<BSPFace> faces { get; set; }
            public BSPPortal Portal { get; set; }
            BSPPlane plane;
            public BSPNode pos;
            public BSPNode neg;

            public bool IsLeaf { get => (neg == null && pos == null); }

            public BSPNode parent;

            public event PropertyChangedEventHandler PropertyChanged;

            public int nodeIdx { get; set; }

            public BSPNode[] Children { get => new BSPNode[2] { neg, pos }; }
            public BSPNode(BSPNode _parent, int _nodeIdx)
            {
                faces = null;
                nodeIdx = _nodeIdx;
                parent = _parent;
            }

            public override string ToString()
            {
                return String.Format("nodeIdx={0}", nodeIdx);
            }
            public BSPNode(BSPNode _parent, BSPFace _f, int _nodeIdx)
            {
                nodeIdx = _nodeIdx;
                faces = new List<BSPFace>() { _f };
                plane = new BSPPlane(_f.PlaneDef, this);
                parent = _parent;
            }

            public void SetPortal(BSPPortal portal, ref int nodeCount)
            {
                portal.parentNode = this;
                this.Portal = portal;
                if (neg != null || pos != null)
                {
                    BSPPortal negPortal, posPortal;
                    portal.SplitFace(plane, out negPortal, out posPortal);
                    if (neg == null && negPortal.Faces.Count >= 4)
                        neg = new BSPNode(this, nodeCount++);
                    if (pos == null && posPortal.Faces.Count >= 4)
                        pos = new BSPNode(this, nodeCount++);
                    if (neg != null)
                        neg.SetPortal(negPortal, ref nodeCount);
                    if (pos != null)
                        pos.SetPortal(posPortal, ref nodeCount);
                }
            }
            public void AddFace(BSPFace inface, ref int nodeIdx)
            {
                if (inface.PlaneDef == plane.Def)
                {
                    faces.Add(inface);
                }
                else
                {
                    uint splitMask = plane.IsSplit(inface);
                    if (splitMask == 1)
                    {
                        if (neg == null)
                            neg = new BSPNode(this, inface, nodeIdx++);
                        else
                            neg.AddFace(inface, ref nodeIdx);
                    }
                    else if (splitMask == 4)
                    {
                        if (pos == null)
                            pos = new BSPNode(this, inface, nodeIdx++);
                        else
                            pos.AddFace(inface, ref nodeIdx);
                    }
                    else if (splitMask == 2)
                    {
                        Debugger.Break();
                        //faces.Add(inface);
                    }
                    else
                    {
                        BSPFace posface, negface;
                        plane.SplitFace(inface, out negface, out posface, null);
                        if ((splitMask & 4) != 0 && posface != null)
                        {
                            if (pos == null)
                                pos = new BSPNode(this, posface, nodeIdx++);
                            else
                                pos.AddFace(posface, ref nodeIdx);
                        }

                        if ((splitMask & 1) != 0 && negface != null)
                        {
                            if (neg == null)
                                neg = new BSPNode(this, negface, nodeIdx++);
                            else
                                neg.AddFace(negface, ref nodeIdx);
                        }
                    }
                }
            }

            public void GetLeafPortals(List<BSPPortal> portals)
            {
                if (IsLeaf)
                {
                    if (this.Portal != null)
                        portals.Add(this.Portal);
                }
                if (neg != null)
                    neg.GetLeafPortals(portals);
                if (pos != null)
                    pos.GetLeafPortals(portals);
            }
            public void ConnectPortals()
            {
                if (plane != null && plane.portalFaces != null)
                {
                    List<Polygon> polys = new List<Polygon>();
                    foreach (var face in plane.portalFaces)
                    {
                        List<Vector2> planepts = plane.ToPlanePts(face.points);
                        polys.Add(new Polygon(planepts));
                    }

                    for (int i = 0; i < polys.Count(); ++i)
                    {
                        for (int j = i+1; j < polys.Count(); ++j)
                        {
                            if (PolygonIntersection.Intersect(polys[i], polys[j]))
                            {
                                if (plane.portalFaces[i].connectedFaces == null)
                                    plane.portalFaces[i].connectedFaces = new List<PortalFace>();
                                if (plane.portalFaces[j].connectedFaces == null)
                                    plane.portalFaces[j].connectedFaces = new List<PortalFace>();
                                plane.portalFaces[i].connectedFaces.Add(plane.portalFaces[j]);
                                plane.portalFaces[j].connectedFaces.Add(plane.portalFaces[i]);
                                //Debug.WriteLine(String.Format("{0} {1}", i, j));
                            }
                        }
                    }
                }
                if (neg != null)
                    neg.ConnectPortals();
                if (pos != null)
                    pos.ConnectPortals();
            }

            public void GetFaces(List<BSPFace> outfaces)
            {
                if (neg != null) neg.GetFaces(outfaces);
                outfaces.AddRange(faces);
                if (pos != null) pos.GetFaces(outfaces);
            }

            public void SetFacePointers()
            {
                if (faces != null)
                {
                    foreach (BSPFace face in faces)
                    {
                        if (face.f != null)
                            face.f.bspNode = this;
                    }
                }
                if (pos != null)
                    pos.SetFacePointers();
                if (neg != null)
                    neg.SetFacePointers();
            }
        }

        public class PlaneMgr
        {
            public KdTree<double, BSPPlaneDef> kdTree =
                new KdTree<double, BSPPlaneDef>(4, new KdTree.Math.DoubleMath());
            int planeIdx = 0;

            public BSPPlaneDef GetPlane(Vector3 normal, double d)
            {
                if (d < 0)
                { d = -d; normal = -normal; }

                var nodes = kdTree.GetNearestNeighbours(new double[] { normal.X, normal.Y, normal.Z,
                        d }, 1);
                if (nodes.Length > 0 && nodes[0].Value.IsEqual(normal, d))
                {
                    return nodes[0].Value;
                }
                else
                {
                    BSPPlaneDef def = new BSPPlaneDef(normal, d, planeIdx++);
                    kdTree.Add(new double[] { def.normal.X, def.normal.Y, def.normal.Z,
                            def.d }, def);
                    return def;
                }

            }
        }
        public class BSPTree
        {
            BSPNode top;
            AABB aabb;
            public BSPNode Top { get => top; }
            
            public void BuildTree(List<Face> faces)
            {
                PlaneMgr mgr = new PlaneMgr();
                int nodeCount = 0;
                List<Vector3> points = new List<Vector3>();
                List<BSPPlaneDef> defs = new List<BSPPlaneDef>();
                List<Tuple<Face, BSPPlaneDef>> addfaces = new List<Tuple<Face, BSPPlaneDef>>();
                foreach (var face in faces)
                {
                    BSPPlaneDef def = mgr.GetPlane(face.Normal,
                            Vector3.Dot(face.edges[0].V0.pt, face.Normal));
                    addfaces.Add(new Tuple<Face, BSPPlaneDef>(face, def));
                    points.AddRange(face.Vtx.Select(v => v.pt));
                }
                aabb = AABB.CreateFromPoints(points);

                top = new BSPNode(null, new BSPFace(addfaces[0].Item2, addfaces[0].Item1), nodeCount++);
                var remaining = addfaces.GetRange(1, addfaces.Count() - 1);
                foreach (var addface in remaining)
                {
                    top.AddFace(new BSPFace(addface.Item2, addface.Item1), ref nodeCount);
                }

                BSPPortal topPortal = new BSPPortal(aabb, mgr);
                top.SetPortal(topPortal, ref nodeCount);
                top.SetFacePointers();
                List<BSPPortal> bSPPortals = new List<BSPPortal>();
                top.GetLeafPortals(bSPPortals);
                foreach (BSPPortal portal in bSPPortals)
                {
                    foreach (PortalFace face in portal.Faces)
                    {
                        if (face.Plane == null)
                            continue;
                        if (face.Plane.portalFaces == null)
                            face.Plane.portalFaces = new List<PortalFace>();
                        face.Plane.portalFaces.Add(face);
                    }
                }

                top.ConnectPortals();
            }

            public List<BSPPortal> GetLeafPortals()
            {
                List<BSPPortal> portals = new List<BSPPortal>();
                top.GetLeafPortals(portals);
                return portals;
            }
            public List<BSPFace> GetFaces()
            {
                List<BSPFace> faces = new List<BSPFace>();
                top.GetFaces(faces);
                return faces;
            }
        }
    }
}
