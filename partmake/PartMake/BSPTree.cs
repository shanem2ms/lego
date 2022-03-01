using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.DoubleNumerics;
using System.ComponentModel;

namespace partmake
{
    namespace Topology
    {

        public class BSPPlane
        {
            public Vector3 Normal;
            public double D;
            Vector3 udir, vdir;
            public Vector3 Origin => Normal * D;

            public BSPPlane(BSPFace f)
            {
                Normal = f.Normal;
                D = Vector3.Dot(f.points[0], Normal);
                GetRefDirs(out udir, out vdir);
            }

            void GetRefDirs(out Vector3 xdir, out Vector3 ydir)
            {
                double vx = Math.Abs(Normal.X);
                double vy = Math.Abs(Normal.Y);
                double vz = Math.Abs(Normal.Z);
                if (vx > vy && vx > vz)
                {
                    // x dominant
                    xdir = Vector3.Cross(Vector3.UnitY, Normal);
                    ydir = Vector3.Cross(xdir, Normal);
                }
                else if (vy > vz)
                {
                    // y dominant
                    xdir = Vector3.Cross(Vector3.UnitX, Normal);
                    ydir = Vector3.Cross(xdir, Normal);
                }
                else
                {
                    // z dominant
                    xdir = Vector3.Cross(Vector3.UnitY, Normal);
                    ydir = Vector3.Cross(xdir, Normal);
                }

                xdir = Vector3.Normalize(xdir);
                ydir = Vector3.Normalize(ydir);
            }
            public List<Vector2> ToPlanePts(IEnumerable<Vector3> pts)
            {
                Vector3 nrm = Normal;
                List<Vector2> v2 = new List<Vector2>();
                Vector3 o = Origin;
                foreach (Vector3 pt in pts)
                {
                    v2.Add(new Vector2(Vector3.Dot((pt - o), udir),
                        Vector3.Dot((pt - o),vdir)));
                }
                return v2;
            }
            public List<Vector3> ToMeshPts(IEnumerable<Vector2> pts)
            {
                Vector3 nrm = Normal;
                List<Vector3> v3 = new List<Vector3>();
                Vector3 o = Origin;
                foreach (Vector2 pt in pts)
                {
                    v3.Add(o + pt.X * udir + pt.Y * vdir);
                }
                return v3;
            }
            public int IsSplit(BSPFace sf)
            {
                int neg = 0, pos = 0, zero = 0;
                foreach (var pt in sf.points)
                {
                    double r = Vector3.Dot(pt - Origin, Normal);
                    if (Eps.Eq(r * 0.01, 0))
                        zero++;
                    else if (r < 0) neg++;
                    else pos++;
                }

                int sgn = 0;
                if (neg > 0) sgn++;
                if (pos > 0) sgn++;
                if (zero > 0) sgn++;
                if (sgn > 1)
                { return 2; }
                else if (neg > 0)
                    return -1;
                else if (pos > 0)
                    return 1;
                else
                    return 0;
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
                    double r = Vector3.Dot(pt - Origin, Normal);
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
                        Vector3 N = Normal;
                        Vector3 edgeDir = Vector3.Normalize(pt1 - pt0);

                        Vector3 ipt = LineIntersection(Origin, Normal, pt0, edgeDir);
                        double dp = Vector3.Dot(ipt - Origin, Normal);
                        pospts.Add(ipt);
                        negpts.Add(ipt);
                        if (splitPts != null)
                            splitPts.Add(ipt);
                    }
                }

                negFace = (negpts.Count > 2) ? new BSPFace(sf.f, negpts) : null;
                posFace = (pospts.Count > 2) ? new BSPFace(sf.f, pospts) : null;
            }

        }
        public class BSPFace
        {
            public Face f;
            public List<Vector3> points;

            public override string ToString()
            {
                return Normal.ToString();
            }

            public Vector3 Normal
            {
                get
                {
                    if (f != null)
                        return f.Normal;
                    else
                        return Vector3.Normalize(Vector3.Cross((points[1] - points[0]),
                        (points[2] - points[1])));
                }
            }
            public BSPFace(Face _f)
            {
                f = _f;
                points = f.Vtx.Select(v => v.pt).ToList();
            }

            public static BSPFace FromPts(BSPPlane p, List<Vector3> inPts)
            {
                List<Vector2> pts = p.ToPlanePts(inPts);
                for (int i = 0; i < pts.Count; ++i)
                {
                    for (int j = i+1; j < pts.Count;)
                    {
                        if (Eps.Eq(pts[i], pts[j]))
                            pts.RemoveAt(j);
                        else
                            ++j;
                    }
                }

                if (pts.Count < 3)
                    return null;

                Vector2 center = new Vector2(0,0);
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

                List<Vector3> meshPts = p.ToMeshPts(spts.Select(v => new Vector2(v.X, v.Y)));
                return new BSPFace(null, meshPts);
            }

            public BSPFace(Face _f, List<Vector3> _pts)
            {
                if (_pts.Count < 3)
                    Debugger.Break();
                f = _f;
                points = _pts;
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

        public class BSPEdge
        {
            public Vector3 v1;
            public Vector3 v2;
        }
        public class BSPPortal
        {
            public List<BSPFace> faces;

            public BSPPortal(AABB aabb)
            {
                faces = new List<BSPFace>()
                {
                    new BSPFace(null, new List<Vector3>()
                    {
                        new Vector3(aabb.Min.X, aabb.Min.Y, aabb.Min.Z),
                        new Vector3(aabb.Min.X, aabb.Min.Y, aabb.Max.Z),
                        new Vector3(aabb.Min.X, aabb.Max.Y, aabb.Max.Z),
                        new Vector3(aabb.Min.X, aabb.Max.Y, aabb.Min.Z)
                    }),
                    new BSPFace(null, new List<Vector3>()
                    {
                        new Vector3(aabb.Max.X, aabb.Min.Y, aabb.Min.Z),
                        new Vector3(aabb.Max.X, aabb.Min.Y, aabb.Max.Z),
                        new Vector3(aabb.Max.X, aabb.Max.Y, aabb.Max.Z),
                        new Vector3(aabb.Max.X, aabb.Max.Y, aabb.Min.Z)
                    }),
                    new BSPFace(null, new List<Vector3>()
                    {
                        new Vector3(aabb.Min.X, aabb.Min.Y, aabb.Min.Z),
                        new Vector3(aabb.Min.X, aabb.Min.Y, aabb.Max.Z),
                        new Vector3(aabb.Max.X, aabb.Min.Y, aabb.Max.Z),
                        new Vector3(aabb.Max.X, aabb.Min.Y, aabb.Min.Z)
                    }),
                    new BSPFace(null, new List<Vector3>()
                    {
                        new Vector3(aabb.Min.X, aabb.Max.Y, aabb.Min.Z),
                        new Vector3(aabb.Min.X, aabb.Max.Y, aabb.Max.Z),
                        new Vector3(aabb.Max.X, aabb.Max.Y, aabb.Max.Z),
                        new Vector3(aabb.Max.X, aabb.Max.Y, aabb.Min.Z)
                    }),
                    new BSPFace(null, new List<Vector3>()
                    {
                        new Vector3(aabb.Min.X, aabb.Min.Y, aabb.Min.Z),
                        new Vector3(aabb.Min.X, aabb.Max.Y, aabb.Min.Z),
                        new Vector3(aabb.Max.X, aabb.Max.Y, aabb.Min.Z),
                        new Vector3(aabb.Max.X, aabb.Min.Y, aabb.Min.Z)
                    }),
                    new BSPFace(null, new List<Vector3>()
                    {
                        new Vector3(aabb.Min.X, aabb.Min.Y, aabb.Max.Z),
                        new Vector3(aabb.Min.X, aabb.Max.Y, aabb.Max.Z),
                        new Vector3(aabb.Max.X, aabb.Max.Y, aabb.Max.Z),
                        new Vector3(aabb.Max.X, aabb.Min.Y, aabb.Max.Z)
                    })
                };
            }

            public BSPPortal(List<BSPFace> _faces)
            {
                faces = _faces;
            }

            public void SplitFace(BSPPlane plane, out BSPPortal negportal, out BSPPortal posportal)
            {
                List<BSPFace> negFaces = new List<BSPFace>();
                List<BSPFace> posFaces = new List<BSPFace>();
                List<Vector3> planePts = new List<Vector3>();
                foreach (BSPFace f in faces)
                {
                    int s = plane.IsSplit(f);
                    if (s == -1)
                        negFaces.Add(f);
                    else if (s == 1)
                        posFaces.Add(f);
                    else if (s == 2)
                    {
                        BSPFace p, n;
                        plane.SplitFace(f, out n, out p, planePts);
                        if (n != null)
                            negFaces.Add(n);
                        if (p != null)
                            posFaces.Add(p);
                    }
                    else if (s == 0)
                    {
                        negFaces.Add(f);
                        posFaces.Add(f);
                    }
                }

                if (planePts.Count > 2)
                {                    
                    BSPFace splitFace = BSPFace.FromPts(plane, planePts);
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
            public bool IsExpanded { get => isExpanded; set { isExpanded = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsExpanded")); } }

            bool isSelected = false;
            public bool IsSelected { get => isSelected; set { isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsSelected")); } }
            public List<BSPFace> faces { get; set; }
            public BSPPortal portal { get; set; }
            BSPPlane plane;
            public BSPNode pos;
            public BSPNode neg;

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
                plane = new BSPPlane(_f);
                parent = _parent;
            }

            public void SetPortal(BSPPortal portal)
            {
                this.portal = portal;
                if (neg != null || pos != null)
                {
                    BSPPortal negPortal, posPortal;
                    portal.SplitFace(plane, out negPortal, out posPortal);
                    if (neg == null && negPortal.faces.Count >= 6)
                        neg = new BSPNode(this, -1);
                    if (pos == null && posPortal.faces.Count >= 6)
                        pos = new BSPNode(this, -1);
                    if (neg != null)
                        neg.SetPortal(negPortal);
                    if (pos != null)
                        pos.SetPortal(posPortal);
                }
            }
            public void AddFace(BSPFace inface, ref int nodeIdx)
            {
                int res = plane.IsSplit(inface);
                if (res == -1)
                {
                    if (neg == null)
                        neg = new BSPNode(this, inface, nodeIdx++);
                    else
                        neg.AddFace(inface, ref nodeIdx);
                }
                else if (res == 1)
                {
                    if (pos == null)
                        pos = new BSPNode(this, inface, nodeIdx++);
                    else
                        pos.AddFace(inface, ref nodeIdx);
                }
                else if (res == 2)
                {
                    BSPFace posface, negface;                    
                    plane.SplitFace(inface, out negface, out posface, null);
                    if (posface != null)
                    {
                        if (pos == null)
                            pos = new BSPNode(this, posface, nodeIdx++);
                        else
                            pos.AddFace(posface, ref nodeIdx);
                    }

                    if (negface != null)
                    {
                        if (neg == null)
                            neg = new BSPNode(this, negface, nodeIdx++);
                        else
                            neg.AddFace(negface, ref nodeIdx);
                    }
                }
                else
                    faces.Add(inface);

            }

            public void GetLeafPortals(List<BSPPortal> portals)
            {
                if (neg == null && pos == null)
                {
                    if (this.portal != null)
                        portals.Add(this.portal);
                }
                if (neg != null)
                    neg.GetLeafPortals(portals);
                if (pos != null)
                    pos.GetLeafPortals(portals);
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

        public class BSPTree
        {
            BSPNode top;
            AABB aabb;
            public BSPNode Top { get => top; }
            public void AddFaces(List<Face> faces)
            {
                int nodeCount = 0;
                List<Vector3> points = new List<Vector3>();
                foreach (var face in faces)
                {
                    points.AddRange(face.Vtx.Select(v => v.pt));
                }
                aabb = AABB.CreateFromPoints(points);

                top = new BSPNode(null, new BSPFace(faces[0]), nodeCount++);
                var remaining = faces.GetRange(1, faces.Count() - 1);
                foreach (Face face in remaining)
                {
                    top.AddFace(new BSPFace(face), ref nodeCount);
                }

                BSPPortal topPortal = new BSPPortal(aabb);
                top.SetPortal(topPortal);
                top.SetFacePointers();
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
