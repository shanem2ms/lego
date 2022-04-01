using System;
using System.Collections.Generic;
using System.DoubleNumerics;

namespace partmake
{
    public struct Vtx
    {
        public const uint SizeInBytes = 32;

        public Vtx(Vector3 p, Vector3 n, Vector2 t)
        { 
            pos = new System.Numerics.Vector3((float)p.X, (float)p.Y, (float)p.Z); 
            nrm = new System.Numerics.Vector3((float)n.X, (float)n.Y, (float)n.Z);
            tx = new System.Numerics.Vector2((float)t.X, (float)t.Y);
        }

        public Vtx(Vector3 p, Vector2 t)
        {
            pos = new System.Numerics.Vector3((float)p.X, (float)p.Y, (float)p.Z);
            nrm = new System.Numerics.Vector3(0,0,1);
            tx = new System.Numerics.Vector2((float)t.X, (float)t.Y);
        }

        public Vtx(System.Numerics.Vector3 p, System.Numerics.Vector3 n, System.Numerics.Vector2 t)
        { pos = p; nrm = n; tx = t; }
        public Vtx(System.Numerics.Vector3 p, System.Numerics.Vector2 t)
        { pos = p; nrm = new System.Numerics.Vector3(0,0,1); tx = t; }
        public System.Numerics.Vector3 pos;
        public System.Numerics.Vector3 nrm;
        public System.Numerics.Vector2 tx;
    }

    public static class MathExt
    {
        public static Vector3 GetScale(this Matrix4x4 mat)
        {
            Vector3 scale, pos;
            Quaternion quat;
            Matrix4x4.Decompose(mat, out scale, out quat, out pos);
            return Vector3.Abs(scale);
        }
    }
    public class LDrawDatNode
    {
        public Matrix4x4 transform;
        public Vector3 WorldScale { get; set; }
        public bool invert;
        public LDrawDatFile File { get; set; }
        public bool IsSelected { get; set; }
        public bool IsEnabled { get; 
            set; } = true;

        public bool Invert => invert;
        public bool IsInverted { get; set; }

        public IEnumerable<Face> Faces => File.Faces;
        public IEnumerable<Topology.Face> TopoFaces => File.TopoFaces;
        public List<string> ReverseLookup => File.ReverseLookup;
        public List<string> IncludedInParts => File.IncludedInParts;

        public Topology.BSPTree BSPTree => File.BSPTree;
        public LDrawDatNode Clone()
        {
            LDrawDatNode c = new LDrawDatNode();
            c.transform = transform;
            c.invert = invert;
            c.File = File.Clone();
            c.IsEnabled = IsEnabled;
            c.IsSelected = false;
            c.IsInverted = IsInverted;
            return c;
        }
    }


    public enum PrimitiveType
    {
        Cylinder,
        Cube,
        Sphere
    }
    public struct Primitive
    {
        public PrimitiveType type;
        public Matrix4x4 transform;
        public bool inverted;
    }

    public class Plane
    {
        public Vector3 nrm;
        public double dist;

        public double DistFromPlane(Vector3 v)
        {
            return Vector3.Dot(v, nrm) - dist;
        }
        public bool IsOnPlane(Vector3 v)
        {            
            return (System.Math.Abs(DistFromPlane(v)) < 0.01);                
        }

        public int FaceSide(Face f)
        {
            const double episilon = 0.01f;
            bool neg = false;
            bool pos = false;
            bool eq = false;
            foreach (Vector3 v in f.v)
            {
                double d = DistFromPlane(v);
                if (d < -episilon)
                {
                    if (pos)
                        return 2;
                    neg = true;
                }
                else if (d > episilon)
                {
                    if (neg)
                        return 2;
                    pos = true;
                }
                else
                    eq = true;
            }
            if (!neg && !pos && eq)
                return 0;
            return pos ? 1 : -1;
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
        public bool IsSelected { get; set; }
        public bool IsEnabled
        {
            get;
            set;
        } = true;

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
            double d = Vector3.Dot(v[0], nrm);                                     
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
        public void GetVertices(List<Vector3> vertices, Matrix4x4 transform, bool inverted)
        {
            Vector3 nrm = Normal(inverted);
            for (int _idx = 0; _idx < v.Length; ++_idx)
            {
                int idx = inverted ? v.Length - _idx - 1 : _idx;
                vertices.Add(V3T(v[idx], transform));
            }
        }

        public void GetTriangleVertices(List<Vtx> vertices, Matrix4x4 transform, bool inverted)
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

        public void Grow(double size)
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
    public static class Eps
    {
        public static double Epsilon = 0.00001;
        public static double Epsilon2 = 0.01;

        public static bool Eq(double a, double b)
        {
            double e = a - b;
            return (e > -Epsilon && e < Epsilon);
        }

        public static bool Eq2(double a, double b)
        {
            double e = a - b;
            return (e > -Epsilon2 && e < Epsilon2);
        }

        public static bool Eq(Vector3 a, Vector3 v)
        {
            return (a - v).LengthSquared() < (Epsilon * 4);
        }
        public static bool Eq(Vector2 a, Vector2 v)
        {
            return (a - v).LengthSquared() < (Epsilon * 4);
        }
    }
}
