using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.DoubleNumerics;

namespace partmake
{
    namespace Topology
    {

        class BSPFace
        {
            public Face f;
            public List<Vector3> points;
            public BSPFace(Face _f)
            {
                f = _f;
                points = f.Vtx.Select(v => v.pt).ToList();
            }

            public BSPFace(Face _f, List<Vector3> _pts)
            {
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

            public int IsSplit(BSPFace sf)
            {
                int neg = 0, pos = 0;
                foreach (var pt in sf.points)
                {
                    double r = Vector3.Dot(pt - points[0], f.Normal);
                    if (Eps.Eq(r, 0))
                        continue;
                    if (r < 0) neg++;
                        else pos++;
                }

                if (neg > 0 && pos > 0)
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
                    System.Diagnostics.Debugger.Break();
                    return Vector3.Zero;
                }

                double t = (Vector3.Dot(planeNormal, planePoint) - Vector3.Dot(planeNormal, linePoint)) / Vector3.Dot(planeNormal, lineDirection);
                return linePoint + lineDirection * t;
            }

            public void SplitFace(BSPFace sf, out BSPFace negFace, out BSPFace posFace)
            {
                int[] posneg = new int[sf.points.Count()];
                int idx = 0;
                foreach (var pt in sf.points)
                {
                    double r = Vector3.Dot(pt - points[0], f.Normal);
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

                    int diff = posneg[(idx + 1) % posneg.Length] -
                        posneg[idx];
                    if (diff == 2 || diff == -2)
                    {
                        Vector3 pt0 = sf.points[idx];
                        Vector3 pt1 = sf.points[(idx + 1) % posneg.Length];
                        Vector3 N = f.Normal;
                        Vector3 edgeDir = Vector3.Normalize(pt1 - pt0);

                        Vector3 ipt = LineIntersection(f.edges[0].V0.pt, f.Normal, pt0, edgeDir);
                        double dp = Vector3.Dot(ipt - points[0], f.Normal);
                        pospts.Add(ipt);
                        negpts.Add(ipt);
                    }
                }

                negFace = new BSPFace(sf.f, negpts);
                if (!negFace.IsValid())
                    Debugger.Break();
                posFace = new BSPFace(sf.f, pospts);
                if (!posFace.IsValid())
                    Debugger.Break();
            }
        }
        class BSPNode
        {
            List<BSPFace> faces;
            BSPFace f => faces.First();

            public BSPNode(BSPFace _f)
            {
                faces = new List<BSPFace>() { _f };
            }

            public void AddFace(BSPFace inface)
            {
                int res = f.IsSplit(inface);
                if (res == -1)
                {
                    if (neg == null)
                        neg = new BSPNode(inface);
                    else
                        neg.AddFace(inface);
                }
                else if (res == 1)
                {
                    if (pos == null)
                        pos = new BSPNode(inface);
                    else
                        pos.AddFace(inface);
                }
                else if (res == 2)
                {
                    BSPFace posface, negface;
                    f.SplitFace(inface, out negface, out posface);
                    if (pos == null)
                        pos = new BSPNode(posface);
                    else
                        pos.AddFace(posface);

                    if (neg == null)
                        neg = new BSPNode(negface);
                    else
                        neg.AddFace(negface);
                }
                else
                    faces.Add(inface);
            }

            public void GetFaces(List<BSPFace> outfaces)
            {
                if (neg != null) neg.GetFaces(outfaces);
                outfaces.AddRange(faces);
                if (pos != null) pos.GetFaces(outfaces);
            }

            public BSPNode pos;
            public BSPNode neg;
        }

        class BSPTree
        {
            BSPNode top;

            public void AddFaces(List<Face> faces)
            {
                top = new BSPNode(new BSPFace(faces[0]));
                var remaining = faces.GetRange(1, faces.Count() - 1);
                foreach (Face face in remaining)
                {
                    top.AddFace(new BSPFace(face));
                }
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
