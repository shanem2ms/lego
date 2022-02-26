using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using constrained_delaunay_triangulation;
using System.DoubleNumerics;

namespace partmake.Topology
{
    public class Triangulator
    { /// <summary>
      /// Determines if the given point is inside the polygon
      /// </summary>
      /// <param name="polygon">the vertices of polygon</param>
      /// <param name="testPoint">the given point</param>
      /// <returns>true if the point is inside the polygon; otherwise, false</returns>
        public static bool IsPointInPolygon4(Vector2[] polygon, Vector2 testPoint)
        {
            bool result = false;
            int j = polygon.Count() - 1;
            for (int i = 0; i < polygon.Count(); i++)
            {
                if (polygon[i].Y < testPoint.Y && polygon[j].Y >= testPoint.Y || polygon[j].Y < testPoint.Y && polygon[i].Y >= testPoint.Y)
                {
                    if (polygon[i].X + (testPoint.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) * (polygon[j].X - polygon[i].X) < testPoint.X)
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;
        }

        public static List<List<Vector3>> Face(Face f)
        {
            List<List<Vertex>> loops = new List<List<Vertex>>();
            List<Edge> edges = f.edges.Select(e => e.e).ToList();
            edges.AddRange(f.interioredges.Select(e => e.e));
            while (edges.Count() > 0)
            {
                Vertex cidx = edges[0].v1;
                Vertex pidx = edges[0].v0;
                List<Vertex> vertices = new List<Vertex>() {
                            pidx, cidx };
                edges.RemoveAt(0);
                bool removed = false;
                bool foundloop = false;
                do
                {
                    removed = false;
                    foreach (var ee in edges)
                    {
                        if (ee.v0.idx == cidx.idx && ee.v1.idx != pidx.idx)
                        {
                            pidx = cidx;
                            cidx = ee.v1;
                            vertices.Add(cidx);
                            edges.Remove(ee);
                            removed = true;
                            if (cidx.idx == vertices[0].idx)
                                foundloop = true;
                            break;
                        }
                        if (ee.v1.idx == cidx.idx && ee.v0.idx != pidx.idx)
                        {
                            pidx = cidx;
                            cidx = ee.v0;
                            vertices.Add(cidx);
                            edges.Remove(ee);
                            removed = true;
                            if (cidx.idx == vertices[0].idx)
                                foundloop = true;
                            break;
                        }
                    }
                } while (removed && !foundloop);
                if (foundloop)
                    loops.Add(vertices);
                else
                    loops[0].AddRange(vertices);
            }
            int surfaceIdx = 0;
            var set_surfaces = new List<pslg_datastructure.surface_store>();
            foreach (var l in loops)
            {
                List<pslg_datastructure.point2d> points2d = new List<pslg_datastructure.point2d>();
                List<pslg_datastructure.edge2d> edges2d = new List<pslg_datastructure.edge2d>();
                var list = l.Select(vtx => vtx.pt).ToList();
                List<Vector2> v2list = f.ToFacePts(list);
                foreach (var vtx in v2list)
                {
                    points2d.Add(new pslg_datastructure.point2d(points2d.Count(), vtx.X, vtx.Y));
                }
                for (int vidx = 0; vidx < v2list.Count - 1; ++vidx)
                {
                    edges2d.Add(new pslg_datastructure.edge2d(vidx, points2d[vidx], points2d[(vidx + 1) % points2d.Count()]));
                }
                pslg_datastructure.surface_store t_surf =
                    new pslg_datastructure.surface_store(surfaceIdx, points2d, edges2d, surfaceIdx);
                surfaceIdx++;
                set_surfaces.Add(t_surf);
            }

            var innerSurfaces =
                set_surfaces.GetRange(1, set_surfaces.Count() - 1).Select(s => s.surface_id).ToList();
            constrained_delaunay_algorithm.create_constrained_mesh(set_surfaces[0].surface_id, innerSurfaces, ref set_surfaces);

            List<List<Vector3>> triangles = new List<List<Vector3>>();
            foreach (var dtri in set_surfaces[0].my_mesh.all_triangles)
            {
                List<Vector3> tri = new List<Vector3>() { 
                                f.ToMeshPt(new Vector2(dtri.vertices[0].x, dtri.vertices[0].y)),
                                f.ToMeshPt(new Vector2(dtri.vertices[1].x, dtri.vertices[1].y)),
                                f.ToMeshPt(new Vector2(dtri.vertices[1].x, dtri.vertices[1].y)) };
                triangles.Add(tri);
            }

            return triangles;
        }
    }
}
