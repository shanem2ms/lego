using System.Collections.Generic;
using System.Numerics;

namespace partmake
{
    public struct Vtx
    {
        public Vtx(Vector3 p, Vector3 n, Vector2 t)
        { pos = p; nrm = n; tx = t; }
        public Vector3 pos;
        public Vector3 nrm;
        public Vector2 tx;
    }
    public class LDrawDatNode
    {
        public Matrix4x4 transform;
        public bool invert;
        public LDrawDatFile File { get; set; }
        public bool IsSelected { get; set; }
    }

    public class Plane
    {
        public Vector3 nrm;
        public float dist;

        public float DistFromPlane(Vector3 v)
        {
            return Vector3.Dot(v, nrm) - dist;
        }
        public bool IsOnPlane(Vector3 v)
        {            
            return (System.MathF.Abs(DistFromPlane(v)) < 0.01f);                
        }
    }
    public class Face
    {
        public Face(int c)
        {
            v = new Vector3[c];
            t = new Vector2[c];
        }
        public Vector3[] v;
        public Vector2[] t;


        Vector3 V3T(Vector3 v, Matrix4x4 m)
        {
            Vector4 v4 = Vector4.Transform(new Vector4(v, 1), m);
            return new Vector3(v4.X, v4.Y, v4.Z);
        }

        public Vector3 Normal(bool inverted)
        {
            Vector3 n = inverted ? Vector3.Cross(v[0] - v[1],
                        v[1] - v[2]) : Vector3.Cross(v[1] - v[0],
                       v[2] - v[1]);
            return Vector3.Normalize(n);
        }

        public Plane GetPlane(bool inverted)
        {
            Vector3 nrm = Normal(inverted);
            float d = Vector3.Dot(v[0], nrm);                                     
            return new Plane() { nrm = nrm, dist = d };
        }

        public void CheckCoPlanar()
        {
            Plane p = GetPlane(false);
            foreach (Vector3 vtx in v)
            {
                if (!p.IsOnPlane(vtx))
                    System.Diagnostics.Debug.WriteLine("Non-conplanar {0}", p.DistFromPlane(vtx));
            }
        }
        public void GetVertices(List<Vtx> vertices, Matrix4x4 transform, bool inverted)
        {
            Vector3 nrm = Normal(inverted);
            if (v.Length == 4)
            {
                if (inverted)
                {
                    nrm = Vector3.TransformNormal(Vector3.Normalize(nrm), transform);
                    vertices.Add(new Vtx(V3T(v[0], transform), nrm, new Vector2(0, 0)));
                    vertices.Add(new Vtx(V3T(v[1], transform), nrm, new Vector2(1, 0)));
                    vertices.Add(new Vtx(V3T(v[2], transform), nrm, new Vector2(1, 1)));
                    vertices.Add(new Vtx(V3T(v[0], transform), nrm, new Vector2(0, 0)));
                    vertices.Add(new Vtx(V3T(v[2], transform), nrm, new Vector2(1, 1)));
                    vertices.Add(new Vtx(V3T(v[3], transform), nrm, new Vector2(1, 0)));
                }
                else
                {
                    nrm = Vector3.TransformNormal(Vector3.Normalize(nrm), transform);
                    vertices.Add(new Vtx(V3T(v[2], transform), nrm, new Vector2(1, 1)));
                    vertices.Add(new Vtx(V3T(v[1], transform), nrm, new Vector2(1, 0)));
                    vertices.Add(new Vtx(V3T(v[0], transform), nrm, new Vector2(0, 0)));
                    vertices.Add(new Vtx(V3T(v[3], transform), nrm, new Vector2(1, 0)));
                    vertices.Add(new Vtx(V3T(v[2], transform), nrm, new Vector2(1, 1)));
                    vertices.Add(new Vtx(V3T(v[0], transform), nrm, new Vector2(0, 0)));
                }
            }
            else if (v.Length == 3)
            {
                if (inverted)
                {
                    nrm = Vector3.TransformNormal(Vector3.Normalize(nrm), transform);
                    vertices.Add(new Vtx(V3T(v[0], transform), nrm, new Vector2(0, 0)));
                    vertices.Add(new Vtx(V3T(v[1], transform), nrm, new Vector2(1, 0)));
                    vertices.Add(new Vtx(V3T(v[2], transform), nrm, new Vector2(1, 1)));
                }
                else
                {
                    nrm = Vector3.TransformNormal(Vector3.Normalize(nrm), transform);
                    vertices.Add(new Vtx(V3T(v[2], transform), nrm, new Vector2(1, 1)));
                    vertices.Add(new Vtx(V3T(v[1], transform), nrm, new Vector2(1, 0)));
                    vertices.Add(new Vtx(V3T(v[0], transform), nrm, new Vector2(0, 0)));
                }
            }
        }
    }
}
