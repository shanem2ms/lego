using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.DoubleNumerics;
using System.ComponentModel;
using System.Text;
using KdTree;

namespace partmake
{
    namespace Topology
    {
        public class BSPFace
        {
            public Face f;
            public List<Vector3> points;
            Plane planedef;
            public Plane PlaneDef
            {
                get => planedef;
            }

            public override string ToString()
            {
                return Normal.ToString();
            }

            public Vector3 Normal => PlaneDef.normal;
            public BSPFace(Plane _p, Face _f)
            {
                planedef = _p;
                f = _f;
                points = f?.Vtx.Select(v => v.pt).ToList();
            }

            public BSPFace(PlaneMgr mgr, List<Vector3> _pts)
            {
                Vector3 nrm = Vector3.Normalize(Vector3.Cross((_pts[1] - _pts[0]),
                        (_pts[2] - _pts[1])));
                double d = Vector3.Dot(_pts[0], nrm);
                planedef = mgr.GetPlane(nrm, d);
                points = _pts;
            }
            public BSPFace(Plane _p, List<Vector3> _pts) :
                this(_p, null, _pts)
            {
            }

            public BSPFace(Plane _p, Face _f, List<Vector3> _pts)
            {
                if (_pts.Count < 3)
                    Debugger.Break();
                Vector3 nrm = Vector3.Normalize(Vector3.Cross((_pts[1] - _pts[0]),
                        (_pts[2] - _pts[1])));
                double d = Vector3.Dot(_pts[0], nrm);

                points = _pts;
                planedef = _p;
                f = _f;
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
        }

        public class PortalFace : BSPFace
        {
            public BSPNode PlaneNode { get; set; }
            public BSPPortal portal;
            bool isExteriorWall = false;
            public bool isFinal = false;
            public bool IsCovered { get; set; } = false;
            public bool IsExteriorWall => isExteriorWall;
            public List<PortalFace> connectedPortalFaces { get; set; }
            public List<BSPFace> connectedModelFaces { get; set; }

            public List<List<Vector3>> modelFaces;

            public bool PointsInward => portal.IsExterior;
            public List<BSPNode> ConnectedNodes
            {
                get
                {
                    return connectedPortalFaces?.Select(p => p.portal.parentNode).ToList();
                }
            }

            public List<Face> ConnectedModelFaces
            {
                get
                {
                    return connectedModelFaces?.Select(f => f.f).ToList();
                }
            }

            public Vector3 Center
            {
                get
                {
                    Vector3 tot = Vector3.Zero;
                    foreach (var pt in points)
                    { tot += pt; }
                    return tot / points.Count;
                }
            }
            public PortalFace(BSPNode node, List<Vector3> _pts) : base(node.Plane, _pts)
            {
                PlaneNode = node;
                TestPts();
            }

            public PortalFace(PortalFace planeFace, List<Vector3> _pts) : base(planeFace.PlaneDef, _pts)
            {
                PlaneNode = planeFace.PlaneNode;
                isExteriorWall = planeFace.isExteriorWall;
                TestPts();
            }
            public PortalFace(PlaneMgr mgr, List<Vector3> _pts) : base(mgr, _pts)
            {
                PlaneNode = null;
                isExteriorWall = true;
                TestPts();
            }

            void TestPts()
            {
                foreach (var pt in this.points)
                {
                    double dist = this.PlaneDef.DistFromPt(pt);
                    if (!Eps.Eq(dist, 0))
                        Debugger.Break();
                }

            }
            public static PortalFace FromPts(BSPNode pl, BSPPortal pr, List<Vector3> inPts, PlaneMgr mgr)
            {
                List<Vector2> pts = pl.Plane.ToPlanePts(inPts);
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
                    spts.Add(mgr.AddPoint(pt.X, pt.Y, Math.Atan2(pt.X - center.X, pt.Y - center.Y)));
                }
                spts.Sort((a, b) => a.Z.CompareTo(b.Z));

                List<Vector3> meshPts = pl.Plane.ToMeshPts(spts.Select(v => new Vector2(v.X, v.Y)));
                return new PortalFace(pl, meshPts);
            }

            public string GenPolyLog()
            {

                StringBuilder sb = new StringBuilder();
                sb.Append("[color=160,160,160]\n");
                {
                    sb.Append("[portalface]\n\n");
                    var pts2d = this.PlaneDef.ToPlanePts(this.points);
                    foreach (var pt in pts2d)
                    {
                        sb.Append($"{pt.X}, {pt.Y}\n");
                    }
                }

                sb.Append("[color=255,128,0]\n");
                foreach (var face in connectedModelFaces)
                {
                    sb.Append($"[face {face.f.idx}]\n\n");
                    var pts2d = this.PlaneDef.ToPlanePts(face.points);
                    foreach (var pt in pts2d)
                    {
                        sb.Append($"{pt.X}, {pt.Y}\n");
                    }

                }


                return sb.ToString();
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
            public System.Numerics.Vector3 color = GenColor();
            public int minhopcount = -1;
            public bool Visible { get; set; } = true;
            public int TraceIdx { get; set; } = -1;

            bool isExterior = false;
            public bool IsExterior => isExterior;
            static Random rColor = new Random();
            static System.Numerics.Vector3 GenColor()
            {
                return new System.Numerics.Vector3(
                    (float)rColor.NextDouble(), (float)rColor.NextDouble(), (float)rColor.NextDouble());
            }
            public BSPPortal(AABB aabb, PlaneMgr mgr)
            {
                faces = new List<PortalFace>()
                {
                    new PortalFace(mgr, new List<Vector3>()
                    {
                        mgr.AddPoint(aabb.Min.X, aabb.Min.Y, aabb.Min.Z),
                        mgr.AddPoint(aabb.Min.X, aabb.Min.Y, aabb.Max.Z),
                        mgr.AddPoint(aabb.Min.X, aabb.Max.Y, aabb.Max.Z),
                        mgr.AddPoint(aabb.Min.X, aabb.Max.Y, aabb.Min.Z)
                    }),
                    new PortalFace(mgr, new List<Vector3>()
                    {
                        mgr.AddPoint(aabb.Max.X, aabb.Min.Y, aabb.Min.Z),
                        mgr.AddPoint(aabb.Max.X, aabb.Min.Y, aabb.Max.Z),
                        mgr.AddPoint(aabb.Max.X, aabb.Max.Y, aabb.Max.Z),
                        mgr.AddPoint(aabb.Max.X, aabb.Max.Y, aabb.Min.Z)
                    }),
                    new PortalFace(mgr, new List<Vector3>()
                    {
                        mgr.AddPoint(aabb.Min.X, aabb.Min.Y, aabb.Min.Z),
                        mgr.AddPoint(aabb.Min.X, aabb.Min.Y, aabb.Max.Z),
                        mgr.AddPoint(aabb.Max.X, aabb.Min.Y, aabb.Max.Z),
                        mgr.AddPoint(aabb.Max.X, aabb.Min.Y, aabb.Min.Z)
                    }),
                    new PortalFace(mgr, new List<Vector3>()
                    {
                        mgr.AddPoint(aabb.Min.X, aabb.Max.Y, aabb.Min.Z),
                        mgr.AddPoint(aabb.Min.X, aabb.Max.Y, aabb.Max.Z),
                        mgr.AddPoint(aabb.Max.X, aabb.Max.Y, aabb.Max.Z),
                        mgr.AddPoint(aabb.Max.X, aabb.Max.Y, aabb.Min.Z)
                    }),
                    new PortalFace(mgr, new List<Vector3>()
                    {
                        mgr.AddPoint(aabb.Min.X, aabb.Min.Y, aabb.Min.Z),
                        mgr.AddPoint(aabb.Min.X, aabb.Max.Y, aabb.Min.Z),
                        mgr.AddPoint(aabb.Max.X, aabb.Max.Y, aabb.Min.Z),
                        mgr.AddPoint(aabb.Max.X, aabb.Min.Y, aabb.Min.Z)
                    }),
                    new PortalFace(mgr, new List<Vector3>()
                    {
                        mgr.AddPoint(aabb.Min.X, aabb.Min.Y, aabb.Max.Z),
                        mgr.AddPoint(aabb.Min.X, aabb.Max.Y, aabb.Max.Z),
                        mgr.AddPoint(aabb.Max.X, aabb.Max.Y, aabb.Max.Z),
                        mgr.AddPoint(aabb.Max.X, aabb.Min.Y, aabb.Max.Z)
                    })
                };
            }
            public BSPPortal(List<PortalFace> _faces)

            {
                faces = _faces.Select(p => new PortalFace(p, p.points)).ToList();
                foreach (var f in faces)
                {
                    f.portal = this;
                }
            }

            public void SplitFace(BSPNode plane, out BSPPortal negportal, out BSPPortal posportal,
                PlaneMgr mgr)
            {
                List<PortalFace> negFaces = new List<PortalFace>();
                List<PortalFace> posFaces = new List<PortalFace>();
                List<Vector3> planePts = new List<Vector3>();
                foreach (PortalFace f in faces)
                {
                    uint s = plane.Plane.IsSplit(f);
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
                        plane.Plane.SplitFace(f, out n, out p, planePts);
                        if ((s & 1) != 0 && n != null)
                            negFaces.Add(new PortalFace(f, n.points));
                        if ((s & 4) != 0 && p != null)
                            posFaces.Add(new PortalFace(f, p.points));
                    }
                }

                if (planePts.Count > 2)
                {
                    PortalFace splitFace = PortalFace.FromPts(plane, this, planePts, mgr);
                    if (splitFace != null)
                    {
                        negFaces.Add(splitFace);
                        posFaces.Add(splitFace);
                    }
                }
                negportal = new BSPPortal(negFaces);
                posportal = new BSPPortal(posFaces);
            }

            IEnumerable<BSPPortal> ConnectedPortals
            {
                get
                {
                    HashSet<BSPPortal> connectedPortals = new HashSet<BSPPortal>();
                    foreach (var face in faces)
                    {
                        if (face.connectedPortalFaces == null)
                            continue;
                        foreach (var cf in face.connectedPortalFaces)
                        {
                            if (cf.portal != this)
                                connectedPortals.Add(cf.portal);
                        }
                    }
                    return connectedPortals;
                }
            }
            public void SetExterior()
            {
                if (isExterior)
                    return;
                isExterior = true;
                foreach (var portal in ConnectedPortals)
                {
                    portal.SetExterior();
                }
            }

            public int TraceToPortal(BSPPortal findPortal, int hopcount)
            {
                int found = -1;
                if (minhopcount < 0 || minhopcount > hopcount)
                {
                    minhopcount = hopcount;
                    if (this == findPortal)
                    {
                        found = hopcount;
                    }
                    else
                    {
                        foreach (var portal in ConnectedPortals)
                        {
                            found = portal.TraceToPortal(findPortal, hopcount + 1);
                            if (found >= 0) break;
                        }
                    }

                }
                if (found >= 0)
                {
                    Visible = true;
                    TraceIdx = found - hopcount;
                    color = new System.Numerics.Vector3((float)hopcount / (float)found,
                        (float)hopcount / (float)found,
                        (float)hopcount / (float)found);
                }
                return found;
            }

            public void SetConnectedInvisible()
            {
                Visible = false;
                foreach (var portal in ConnectedPortals)
                {
                    portal.Visible = false;
                }
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
                    if (isExpanded == true && parent != null) parent.IsExpanded = true;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsExpanded"));
                }
            }

            bool isSelected = false;
            public bool IsSelected { get => isSelected; set { isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsSelected")); } }
            public List<BSPFace> faces { get; set; }
            public BSPPortal Portal { get; set; }
            public Plane Plane;
            public BSPNode pos;
            public BSPNode neg;
            public List<PortalFace> portalFaces;

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

            public BSPNode FromIdx(int idx)
            {
                if (idx == nodeIdx)
                    return this;
                else
                {                    
                    BSPNode node = this.pos?.FromIdx(idx);
                    if (node != null) return node;
                    node = this.neg?.FromIdx(idx);
                    if (node != null) return node;
                }
                return null;
            }
            public override string ToString()
            {
                return String.Format("nodeIdx={0}", nodeIdx);
            }
            public BSPNode(BSPNode _parent, BSPFace _f, int _nodeIdx)
            {
                nodeIdx = _nodeIdx;
                faces = new List<BSPFace>() { _f };
                if (_f.f != null)
                {
                    if (_f.f.bspNodes == null)
                        _f.f.bspNodes = new List<BSPNode>();
                    _f.f.bspNodes.Add(this);
                }
                Plane = _f.PlaneDef;
                parent = _parent;
            }

            public void SetPortal(BSPPortal portal, PlaneMgr mgr, ref int nodeCount)
            {
                //if (this == )
                portal.parentNode = this;
                this.Portal = portal;
                BSPPortal negPortal, posPortal;
                portal.SplitFace(this, out negPortal, out posPortal, mgr);
                if (neg != null)
                    neg.SetPortal(negPortal, mgr, ref nodeCount);
                if (pos != null)
                    pos.SetPortal(posPortal, mgr, ref nodeCount);

                if (neg == null && negPortal.Faces.Count >= 4)
                {
                    neg = new BSPNode(this, nodeCount++);
                    negPortal.parentNode = neg;
                    neg.Portal = negPortal;
                }
                if (pos == null && posPortal.Faces.Count >= 4)
                {
                    pos = new BSPNode(this, nodeCount++);
                    posPortal.parentNode = pos;
                    pos.Portal = posPortal;
                }

            }
            public void AddFace(BSPFace inface, ref int nodeIdx)
            {
                if (inface.PlaneDef == Plane)
                {
                    if (inface.f.bspNodes == null)
                        inface.f.bspNodes = new List<BSPNode>();
                    inface.f.bspNodes.Add(this);
                    faces.Add(inface);
                }
                else
                {
                    uint splitMask = Plane.IsSplit(inface);
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
                        //Debugger.Break();
                        if (inface.f.bspNodes == null)
                            inface.f.bspNodes = new List<BSPNode>();
                        inface.f.bspNodes.Add(this);
                        faces.Add(inface);
                    }
                    else
                    {
                        BSPFace posface, negface;
                        Plane.SplitFace(inface, out negface, out posface, null);
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

            int debugNodeIdx = -1;
            public void ConnectPortals()
            {
                if (portalFaces != null)
                {
                    if (nodeIdx == debugNodeIdx)
                        Debugger.Break();
                    PointMgr mgr = new PointMgr();
                    PolygonClip clipper = new PolygonClip();
                    List<Polygon> portalPolys = new List<Polygon>();
                    foreach (var face in portalFaces)
                    {
                        List<Vector2> planepts = Plane.ToPlanePts(face.points).Select(p => new Vector2(
                            mgr.AddPoint(p.X), mgr.AddPoint(p.Y))).ToList();
                        if (nodeIdx == debugNodeIdx)
                        {
                            foreach (var p in planepts)
                            {
                                Debug.WriteLine($"{p.X},{p.Y}");
                            }
                            Debug.WriteLine("");
                        }
                        var poly = new Polygon(planepts);
                        portalPolys.Add(poly);
                        clipper.AddPortalPolygon(poly, face.portal.parentNode.nodeIdx);
                    }
                    List<Polygon> modelPolys = new List<Polygon>();

                    if (nodeIdx == debugNodeIdx)
                    {
                        Debug.WriteLine("[models]");
                    }
                    List<BSPFace> modelFaces = new List<BSPFace>();
                    foreach (var face in this.faces)
                    {
                        List<Vector2> planepts = Plane.ToPlanePts(face.points).Select(p => new Vector2(
                            mgr.AddPoint(p.X), mgr.AddPoint(p.Y))).ToList();
                        if (nodeIdx == debugNodeIdx)
                        {
                            foreach (var p in planepts)
                            {
                                Debug.WriteLine($"{p.X},{p.Y}");
                            }
                            Debug.WriteLine("");
                        }
                        var poly = new Polygon(planepts);
                        modelPolys.Add(poly);
                        clipper.AddModelPolygon(poly, face.f.idx);
                        if (face.f != null)
                            modelFaces.Add(face);
                    }

                    Tuple<int, int>[] connecedPolys = clipper.Process(nodeIdx);
                    List<PolygonClip.GetIntersectedPolyResult> results =
                        clipper.GetIntersectedPolys();
                    foreach (var result in results)
                    {
                        if (portalFaces[result.p1].modelFaces == null)
                            portalFaces[result.p1].modelFaces = new List<List<Vector3>>();
                        if (portalFaces[result.p2].modelFaces == null)
                            portalFaces[result.p2].modelFaces = new List<List<Vector3>>();
                        var modelFace = Plane.ToMeshPts(result.poly.Vertices).ToList();
                        portalFaces[result.p1].modelFaces.Add(modelFace);
                        portalFaces[result.p2].modelFaces.Add(modelFace);
                    }

                    foreach (var tuple in connecedPolys)
                    {
                        int i = tuple.Item1;
                        int j = tuple.Item2;
                        if (portalFaces[i].connectedPortalFaces == null)
                            portalFaces[i].connectedPortalFaces = new List<PortalFace>();
                        if (portalFaces[j].connectedPortalFaces == null)
                            portalFaces[j].connectedPortalFaces = new List<PortalFace>();

                        portalFaces[i].connectedPortalFaces.Add(portalFaces[j]);
                        portalFaces[j].connectedPortalFaces.Add(portalFaces[i]);
                    }

                    /*
                    int[] coveredPortalFaces = clipper.Process2(nodeIdx);

                    foreach (int i in coveredPortalFaces)
                    {
                        portalFaces[i].IsCovered = true;
                    }
                    */
                    if (modelFaces.Count > 0)
                    {
                        foreach (var face in portalFaces)
                        {
                            if (face.connectedModelFaces == null)
                            {
                                face.connectedModelFaces = new List<BSPFace>();
                                face.connectedModelFaces.AddRange(modelFaces);

                                foreach (var mf in modelFaces)
                                {
                                    if (mf.f.portalFaces == null) mf.f.portalFaces = new List<PortalFace>();
                                    mf.f.portalFaces.Add(face);
                                }
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
        }

        public class PointMgr
        {
            static double Epsilon = Mesh.Epsilon;
            List<double> vals = new List<double>();
            public double AddPoint(double x)
            {
                int idx = vals.BinarySearch(x);
                if (idx >= 0)
                    return vals[idx];
                else
                {
                    idx = ~idx;
                    if (idx >= vals.Count)
                    {
                        vals.Add(x);
                        return x;
                    }
                    else
                    {
                        double tx = vals[idx];
                        double dx = tx - x;
                        if (dx < Epsilon)
                            return tx;
                        else if (idx > 0 && (x - vals[idx - 1]) < Epsilon)
                            return vals[idx - 1];
                        else
                        {
                            vals.Insert(idx, x);
                            return x;
                        }
                    }
                }
            }
        }
        public class BSPTree
        {
            BSPNode top;
            AABB aabb;
            public BSPNode Top { get => top; }
            BSPPortal startPortal;
            public void BuildTree(List<Face> faces, PlaneMgr mgr)
            {
                if (faces.Count == 0)
                    return;

                int nodeCount = 0;
                List<Vector3> points = new List<Vector3>();
                List<Plane> defs = new List<Plane>();
                List<Tuple<Face, Plane>> addfaces = new List<Tuple<Face, Plane>>();

                foreach (var face in faces)
                {
                    Plane def = face.Plane;
                    addfaces.Add(new Tuple<Face, Plane>(face, def));
                    points.AddRange(face.Vtx.Select(v => mgr.AddPoint(v.pt.X, v.pt.Y, v.pt.Z)));
                }
                aabb = AABB.CreateFromPoints(points);
                aabb.Grow(1);

                top = new BSPNode(null, new BSPFace(addfaces[0].Item2, addfaces[0].Item1), nodeCount++);
                var remaining = addfaces.GetRange(1, addfaces.Count() - 1);
                foreach (var addface in remaining)
                {
                    top.AddFace(new BSPFace(addface.Item2, addface.Item1), ref nodeCount);
                }

                BSPPortal topPortal = new BSPPortal(aabb, mgr);
                top.SetPortal(topPortal, mgr, ref nodeCount);

                {
                    List<BSPPortal> bSPPortals = new List<BSPPortal>();
                    top.GetLeafPortals(bSPPortals);
                    foreach (BSPPortal portal in bSPPortals)
                    {
                        foreach (PortalFace face in portal.Faces)
                        {
                            if (face.PlaneNode == null)
                                continue;
                            if (face.PlaneNode.portalFaces == null)
                                face.PlaneNode.portalFaces = new List<PortalFace>();
                            face.PlaneNode.portalFaces.Add(face);
                        }
                    }
                }
                top.ConnectPortals();
                {
                    List<BSPPortal> bSPPortals = new List<BSPPortal>();
                    top.GetLeafPortals(bSPPortals);

                    foreach (BSPPortal portal in bSPPortals)
                    {
                        foreach (PortalFace face in portal.Faces)
                        {
                            if (face.IsExteriorWall && face.ConnectedModelFaces == null)
                            {
                                startPortal = portal;
                                break;
                            }
                        }
                        if (startPortal != null)
                            break;
                    }

                    startPortal.SetExterior();
                }
                {
                    List<BSPPortal> bSPPortals = new List<BSPPortal>();
                    top.GetLeafPortals(bSPPortals);

                    foreach (BSPPortal portal in bSPPortals)
                    {
                        if (portal.parentNode.nodeIdx == 244)
                            Debugger.Break();
                        foreach (PortalFace face in portal.Faces)
                        {
                            bool ext = face.portal.IsExterior;
                            bool isFinal = true;
                            if (face.connectedPortalFaces == null)
                                continue;
                            foreach (var connected in face.connectedPortalFaces)
                            {
                                if (connected.portal.IsExterior == ext)
                                { isFinal = false; break; }
                            }
                            face.isFinal = isFinal;
                        }
                    }
                }
            }
            public BSPNode FromIdx(int idx)
            {
                return this.top.FromIdx(idx);
            }

            public void TraceToPortal(BSPPortal p)
            {
                List<BSPPortal> bSPPortals = new List<BSPPortal>();
                top.GetLeafPortals(bSPPortals);
                foreach (BSPPortal portal in bSPPortals)
                {
                    portal.minhopcount = -1;
                }
                startPortal.TraceToPortal(p, 0);
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
