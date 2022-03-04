using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DoubleNumerics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace partmake
{
    namespace Topology
    {
        public class Intersection
        {
            public Edge e1;
            public Edge e2;
            public Vector3 pt;
        }

        struct OutIntersection
        {
            public Vector3 pt;
            public int e1;
            public int e2;
        }
        public class EdgeIntersectCPP
        {
            [DllImport("EdgeIntersect.dll")]
            static extern int FindAllIntersections(IntPtr vertices, int vtxCount, IntPtr edges, int edgeCount,
                IntPtr outIntersections, int maxIntersectionCnt);
            public List<Intersection> FindAllIntersections(List<Vertex> vertices, List<Edge> edges, Mesh.LogDel logdel)
            {
                int v3size = Marshal.SizeOf<Vector3>();
                IntPtr ptr = Marshal.AllocHGlobal(vertices.Count() * v3size);

                IntPtr curptr = ptr;
                foreach (Vertex v in vertices)
                {
                    Marshal.StructureToPtr<Vector3>(v.pt, curptr, false);
                    curptr = IntPtr.Add(curptr, v3size);
                }
                IntPtr edgeptr = Marshal.AllocHGlobal(sizeof(int) * edges.Count * 2);
                curptr = edgeptr;
                foreach (Edge e in edges)
                {
                    byte[] byteint = BitConverter.GetBytes(e.v0.idx);
                    Marshal.Copy(byteint, 0, curptr, byteint.Length);
                    curptr = IntPtr.Add(curptr, byteint.Length);
                    byteint = BitConverter.GetBytes(e.v1.idx);
                    Marshal.Copy(byteint, 0, curptr, byteint.Length);
                    curptr = IntPtr.Add(curptr, byteint.Length);
                }

                int maxIntersections = 65536;
                int oiSize = Marshal.SizeOf<OutIntersection>();
                IntPtr outOIptr = Marshal.AllocHGlobal(oiSize * maxIntersections);
                int intersectionCnt = FindAllIntersections(ptr, vertices.Count, edgeptr, edges.Count,
                    outOIptr, maxIntersections);
                int outCnt = Math.Min(intersectionCnt, maxIntersections);
                IntPtr outOICur = outOIptr;
                List<Intersection> outIntersections = new List<Intersection>();
                for (int i = 0; i < outCnt; ++i)
                {
                    OutIntersection oi =
                        Marshal.PtrToStructure<OutIntersection>(outOICur);
                    outOICur = IntPtr.Add(outOICur, oiSize);
                    outIntersections.Add(new Intersection()
                    {
                        e1 = edges[oi.e1],
                        e2 = edges[oi.e2],
                        pt = oi.pt
                    });
                }
                Marshal.FreeHGlobal(ptr);
                Marshal.FreeHGlobal(edgeptr);
                Marshal.FreeHGlobal(outOIptr);
                return outIntersections;
            }
        }

        public class PolygonClip
        {
            [DllImport("EdgeIntersect.dll")]
            static extern int ClipPolygons(IntPtr pointList, IntPtr polyPointCounts, int nPortalPolys, int nModelPolys,
                IntPtr outConnectedPortals);

            List<Polygon> portalPolys = new List<Polygon>();
            List<Polygon> modelPolys = new List<Polygon>();
            public void AddPortalPolygon(Polygon p)
            {
                portalPolys.Add(p);
            }
            public void AddModelPolygon(Polygon p)
            {
                modelPolys.Add(p);
            }

            public Tuple<int,int> []Process()
            {
                List<Vector2> points = new List<Vector2>();
                List<int> polyOffsets = new List<int>();
                int pointCout = 0;
                foreach (Polygon p in portalPolys)
                {
                    pointCout += p.GetVertices().Length;
                    polyOffsets.Add(pointCout);
                }
                foreach (Polygon p in modelPolys)
                {
                    pointCout += p.GetVertices().Length;
                    polyOffsets.Add(pointCout);
                }

                int v2size = Marshal.SizeOf<Vector2>();
                IntPtr ptr = Marshal.AllocHGlobal(pointCout * v2size);
                IntPtr ptrSizes = Marshal.AllocHGlobal(polyOffsets.Count * sizeof(int));
                IntPtr outConnectedPortals = Marshal.AllocHGlobal(portalPolys.Count * portalPolys.Count * sizeof(int) * 2);
                Marshal.Copy(polyOffsets.ToArray(), 0, ptrSizes, polyOffsets.Count);

                IntPtr curptr = ptr;
                foreach (Polygon p in portalPolys)
                {
                    var vertices = p.GetVertices();
                    foreach (var v in vertices)
                    {
                        Marshal.StructureToPtr<Vector2>(v, curptr, false);
                        curptr = IntPtr.Add(curptr, v2size);
                    }
                }
                foreach (Polygon p in modelPolys)
                {
                    var vertices = p.GetVertices();
                    foreach (var v in vertices)
                    {
                        Marshal.StructureToPtr<Vector2>(v, curptr, false);
                        curptr = IntPtr.Add(curptr, v2size);
                    }
                }

                int connectedPortalCount =
                    ClipPolygons(ptr, ptrSizes, portalPolys.Count, modelPolys.Count, outConnectedPortals);
                int []connectedPortals = new int [connectedPortalCount * 2];
                Marshal.Copy(outConnectedPortals, connectedPortals, 0, connectedPortalCount * 2);
                Marshal.FreeHGlobal(ptr);
                Marshal.FreeHGlobal(ptrSizes);
                Marshal.FreeHGlobal(outConnectedPortals);

                Tuple<int, int>[] tuples = new Tuple<int, int>[connectedPortalCount];
                for (int i = 0; i < tuples.Length; i++)
                {
                    tuples[i] = new Tuple<int, int>(connectedPortals[i * 2],
                        connectedPortals[i * 2 + 1]);
                }
                return tuples;
            }
        }
    }
}
