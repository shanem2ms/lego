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
                List<Intersection>  outIntersections = new List<Intersection>();
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
    }
}
