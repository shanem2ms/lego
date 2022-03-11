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


        public Vector2[] Vertices => vertices;

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
