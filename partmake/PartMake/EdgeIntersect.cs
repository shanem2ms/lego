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
            static extern int ClipPolygons(int nodeIdx, IntPtr pointList, IntPtr polyPointCounts, IntPtr idxs, int nPortalPolys, int nModelPolys,
                IntPtr outConnectedPortals);
            [DllImport("EdgeIntersect.dll")]
            static extern int ClipPolygons2(int nodeIdx, IntPtr pointList, IntPtr polyPointCounts, IntPtr idxs, int nPortalPolys, int nModelPolys,
                IntPtr outConnectedPortals);

            [DllImport("EdgeIntersect.dll")]
            static extern int GetLogLength();

            [DllImport("EdgeIntersect.dll")]
            static extern int GetLogBytes(IntPtr logBytes);

            [DllImport("EdgeIntersect.dll")]
            static extern void SetLogNodeIdx(int idx);

            [DllImport("EdgeIntersect.dll")]
            static extern int GetIntPolys();

            [DllImport("EdgeIntersect.dll")]
            static extern IntPtr GetIntPolysPtr();

            [DllImport("EdgeIntersect.dll")]
            static extern IntPtr GetIntPolysMapPtr();

            List<Polygon> portalPolys = new List<Polygon>();
            List<Polygon> modelPolys = new List<Polygon>();
            List<int> nodeIdxs = new List<int>();
            List<int> faceIdxs = new List<int>();
            public void AddPortalPolygon(Polygon p, int nodeIdx)
            {
                portalPolys.Add(p);
                nodeIdxs.Add(nodeIdx);
            }
            public void AddModelPolygon(Polygon p, int faceIdx)
            {
                modelPolys.Add(p);
                faceIdxs.Add(faceIdx);
            }

            public static void SetLogIdx(int idx, bool planes)
            {
                SetLogNodeIdx(planes ? idx << 16 : idx);
            }
            public static string GetLog()
            {
                int logLen = GetLogLength();
                if (logLen == 0)
                    return null;
                IntPtr ptr = Marshal.AllocHGlobal(logLen);
                GetLogBytes(ptr);
                string ret = Marshal.PtrToStringAnsi(ptr);
                Marshal.FreeHGlobal(ptr);
                return ret;
            }
            public Tuple<int,int> []Process(int nodeIdx)
            {
                List<Vector2> points = new List<Vector2>();
                List<int> polyOffsets = new List<int>();
                int pointCout = 0;
                foreach (Polygon p in portalPolys)
                {
                    pointCout += p.Vertices.Length;
                    polyOffsets.Add(pointCout);                    
                }
                foreach (Polygon p in modelPolys)
                {
                    pointCout += p.Vertices.Length;
                    polyOffsets.Add(pointCout);
                }

                int v2size = Marshal.SizeOf<Vector2>();
                IntPtr ptr = Marshal.AllocHGlobal(pointCout * v2size);
                IntPtr ptrSizes = Marshal.AllocHGlobal(polyOffsets.Count * sizeof(int));
                IntPtr outConnectedPortals = Marshal.AllocHGlobal(portalPolys.Count * portalPolys.Count * sizeof(int) * 2);
                Marshal.Copy(polyOffsets.ToArray(), 0, ptrSizes, polyOffsets.Count);
                List<int> indices = new List<int>();
                indices.AddRange(this.nodeIdxs);
                indices.AddRange(this.faceIdxs);
                IntPtr idxs = Marshal.AllocHGlobal(polyOffsets.Count * sizeof(int));
                Marshal.Copy(indices.ToArray(), 0, idxs, indices.Count);
                IntPtr curptr = ptr;
                foreach (Polygon p in portalPolys)
                {
                    var vertices = p.Vertices;
                    foreach (var v in vertices)
                    {
                        Marshal.StructureToPtr<Vector2>(v, curptr, false);
                        curptr = IntPtr.Add(curptr, v2size);
                    }
                }
                foreach (Polygon p in modelPolys)
                {
                    var vertices = p.Vertices;
                    foreach (var v in vertices)
                    {
                        Marshal.StructureToPtr<Vector2>(v, curptr, false);
                        curptr = IntPtr.Add(curptr, v2size);
                    }
                }

                int connectedPortalCount =
                    ClipPolygons(nodeIdx, ptr, ptrSizes, idxs, portalPolys.Count, modelPolys.Count, outConnectedPortals);
                int []connectedPortals = new int [connectedPortalCount * 2];
                Marshal.Copy(outConnectedPortals, connectedPortals, 0, connectedPortalCount * 2);
                Marshal.FreeHGlobal(ptr);
                Marshal.FreeHGlobal(ptrSizes);
                Marshal.FreeHGlobal(idxs);                
                Marshal.FreeHGlobal(outConnectedPortals);

                Tuple<int, int>[] tuples = new Tuple<int, int>[connectedPortalCount];
                for (int i = 0; i < tuples.Length; i++)
                {
                    tuples[i] = new Tuple<int, int>(connectedPortals[i * 2],
                        connectedPortals[i * 2 + 1]);
                }

                return tuples;
            }

            public class GetIntersectedPolyResult
            {
                public int p1;
                public int p2;
                public Polygon poly;
            }
            public List<GetIntersectedPolyResult> GetIntersectedPolys()
            {
                List<GetIntersectedPolyResult> results = new List<GetIntersectedPolyResult>();
                int numPolys = GetIntPolys();
                IntPtr polyMaps = GetIntPolysMapPtr();
                int[] polyMapArray = new int[numPolys];
                Marshal.Copy(polyMaps, polyMapArray, 0, polyMapArray.Length);
                int numpts = 0;
                for (int i = 0; i < polyMapArray.Length; i += 3)
                {
                    numpts += polyMapArray[i + 2];
                }
                IntPtr ptsPtr = GetIntPolysPtr();
                double[] pts = new double[numpts * 2];
                Marshal.Copy(ptsPtr, pts, 0, pts.Length);

                int offset = 0;
                for (int i = 0; i < polyMapArray.Length; i += 3)
                {
                    GetIntersectedPolyResult res = new GetIntersectedPolyResult();
                    res.p1 = polyMapArray[i];
                    res.p2 = polyMapArray[i+1];
                    int numpolypts = polyMapArray[i + 2];
                    List<Vector2> pts2 = new List<Vector2>();
                    for (int j = offset; j < offset + numpolypts; j++)
                    {
                        pts2.Add(new Vector2(pts[j * 2], pts[j * 2 + 1]));
                    }
                    res.poly = new Polygon(pts2);
                    offset += numpolypts;
                    results.Add(res);
                }
                return results;
            }
            public int[] Process2(int nodeIdx)
            {
                List<int> polyOffsets = new List<int>();
                int pointCout = 0;
                foreach (Polygon p in portalPolys)
                {
                    pointCout += p.Vertices.Length;
                    polyOffsets.Add(pointCout);
                }
                foreach (Polygon p in modelPolys)
                {
                    pointCout += p.Vertices.Length;
                    polyOffsets.Add(pointCout);
                }

                int v2size = Marshal.SizeOf<Vector2>();
                IntPtr ptr = Marshal.AllocHGlobal(pointCout * v2size);
                IntPtr ptrSizes = Marshal.AllocHGlobal(polyOffsets.Count * sizeof(int));
                IntPtr outConnectedPortals = Marshal.AllocHGlobal(portalPolys.Count * portalPolys.Count * sizeof(int) * 2);
                Marshal.Copy(polyOffsets.ToArray(), 0, ptrSizes, polyOffsets.Count);
                List<int> indices = new List<int>();
                indices.AddRange(this.nodeIdxs);
                indices.AddRange(this.faceIdxs);
                IntPtr idxs = Marshal.AllocHGlobal(polyOffsets.Count * sizeof(int));
                Marshal.Copy(indices.ToArray(), 0, idxs, indices.Count);
                IntPtr curptr = ptr;
                foreach (Polygon p in portalPolys)
                {
                    var vertices = p.Vertices;
                    foreach (var v in vertices)
                    {
                        Marshal.StructureToPtr<Vector2>(v, curptr, false);
                        curptr = IntPtr.Add(curptr, v2size);
                    }
                }
                foreach (Polygon p in modelPolys)
                {
                    var vertices = p.Vertices;
                    foreach (var v in vertices)
                    {
                        Marshal.StructureToPtr<Vector2>(v, curptr, false);
                        curptr = IntPtr.Add(curptr, v2size);
                    }
                }

                int connectedPortalCount =
                    ClipPolygons2(nodeIdx, ptr, ptrSizes, idxs, portalPolys.Count, modelPolys.Count, outConnectedPortals);
                int[] coveredPortalFaces = new int[connectedPortalCount];
                Marshal.Copy(outConnectedPortals, coveredPortalFaces, 0, connectedPortalCount);
                Marshal.FreeHGlobal(ptr);
                Marshal.FreeHGlobal(ptrSizes);
                Marshal.FreeHGlobal(idxs);
                Marshal.FreeHGlobal(outConnectedPortals);

                return coveredPortalFaces;
            }
        }
    }
}
