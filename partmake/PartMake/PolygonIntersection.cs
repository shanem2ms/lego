using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.DoubleNumerics;

namespace partmake
{
    public class Polygon
    {

        private readonly Vector2[] vertices;
        public PolyAABB aabb;

        public Polygon(IEnumerable<Vector2> _vertices)
        {
            this.aabb = new PolyAABB(_vertices);
            this.vertices = _vertices.ToArray();
        }


        public Vector2[] GetVertices()
        {
            return vertices;
        }

        public Vector2 GetVertex(int index)
        {
            return vertices[index];
        }

        public Vector2 GetEdge(int index)
        {
            Vector2 v1 = vertices[index];
            Vector2 v2 = vertices[(index + 1) % vertices.Length];
            return v1 - v2;
        }

        public Vector2 GetEdgeNormal(int index)
        {
            Vector2 edge = GetEdge(index);
            return new Vector2(edge.Y, -edge.X);
        }

        public List<Vector2> GetEdges()
        {
            List<Vector2> edges = new List<Vector2>();
            for (int i = 0; i < vertices.Length; i++)
            {
                edges.Add(GetEdge(i));
            }
            return edges;
        }

        public List<Vector2> GetEdgeNormals()
        {
            List<Vector2> normals = new List<Vector2>();
            for (int i = 0; i < vertices.Length; i++)
            {
                normals.Add(GetEdgeNormal(i));
            }
            return normals;
        }
    }

    public static class PolygonIntersection
    {
        public static bool Intersect(Polygon polygon1, Polygon polygon2)
        {
            if (!polygon1.aabb.Intersects(polygon2.aabb))
                return false;
            List<Vector2> normals = new List<Vector2>();
            normals.AddRange(polygon1.GetEdgeNormals());
            normals.AddRange(polygon2.GetEdgeNormals());
            foreach (Vector2 axis in normals)
            {
                var (min1, max1) = GetMinMaxProjections(polygon1, axis);
                var (min2, max2) = GetMinMaxProjections(polygon2, axis);
                double intervalDistance = min1 < min2 ? min2 - max1 : min1 - max2;
                if (intervalDistance >= 0) return false;
            }
            return true;
        }

        private static (double, double) GetMinMaxProjections(Polygon polygon, Vector2 axis)
        {
            double min = Int32.MaxValue;
            double max = Int32.MinValue;
            foreach (Vector2 vertex in polygon.GetVertices())
            {
                Vector2 projection = Project(vertex, axis);
                double scalar = Scalar(projection, axis);
                if (scalar < min) min = scalar;
                if (scalar > max) max = scalar;
            }
            return (min, max);
        }

        private static Vector2 Project(Vector2 vertex, Vector2 axis)
        {
            double dot = Vector2.Dot(vertex, axis);
            double mag2 = axis.LengthSquared();
            return dot / mag2 * axis;
        }

        private static double Scalar(Vector2 vertex, Vector2 axis)
        {
            return Vector2.Dot(vertex, axis);
        }

    }

    public class PolyAABB
    {
        Vector2 max;
        Vector2 min;
        public PolyAABB(IEnumerable<Vector2> vecs)
        {
            max = min = vecs.First();
            foreach (Vector2 v in vecs)
            {
                if (v.X < min.X) min.X = v.X;
                if (v.Y < min.Y) min.Y = v.Y;
                if (v.X > max.X) max.X = v.X;
                if (v.Y > max.Y) max.Y = v.Y;
            }
        }

        public bool Intersects(PolyAABB other)
        {
            if (other.min.X > max.X ||
                other.min.Y > max.Y ||
                other.max.X < min.X ||
                other.max.Y < min.Y)
                return false;
            return true;
        }
    }
}
