using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DoubleNumerics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;
using FL = System.Numerics;

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
            static extern int UnionPolygons(IntPtr pointListDbls, IntPtr polyPointCounts, int nPortalPolys, IntPtr outPointList,
                IntPtr outPolyPointCounts);

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

            public struct ConnectedFace
            {
                public int face0;
                public int face1;
                public int blocked;
            }
            public ConnectedFace[]Process(int nodeIdx)
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
                IntPtr outConnectedPortals = Marshal.AllocHGlobal(portalPolys.Count * portalPolys.Count * sizeof(int) * 3);
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
                int []connectedPortals = new int [connectedPortalCount * 3];
                Marshal.Copy(outConnectedPortals, connectedPortals, 0, connectedPortalCount * 3);
                Marshal.FreeHGlobal(ptr);
                Marshal.FreeHGlobal(ptrSizes);
                Marshal.FreeHGlobal(idxs);                
                Marshal.FreeHGlobal(outConnectedPortals);

                ConnectedFace[] cfaces = new ConnectedFace[connectedPortalCount];
                for (int i = 0; i < connectedPortalCount; i++)
                {
                    cfaces[i] = new ConnectedFace()
                    {
                        face0 = connectedPortals[i * 3],
                        face1 = connectedPortals[i * 3 + 1],
                        blocked = connectedPortals[i * 3 + 2]
                    };
                }

                return cfaces;
            }

            public List<Polygon> CombinePolys()
            {
                List<int> polyOffsets = new List<int>();
                int pointCout = 0;
                foreach (Polygon p in portalPolys)
                {
                    pointCout += p.Vertices.Length;
                    polyOffsets.Add(pointCout);
                }

                int v2size = Marshal.SizeOf<Vector2>();
                IntPtr ptr = Marshal.AllocHGlobal(pointCout * v2size);
                IntPtr ptrSizes = Marshal.AllocHGlobal(polyOffsets.Count * sizeof(int));
                IntPtr outPolyPoints = Marshal.AllocHGlobal(pointCout * v2size);
                IntPtr outptrSizes = Marshal.AllocHGlobal(polyOffsets.Count * sizeof(int));
                Marshal.Copy(polyOffsets.ToArray(), 0, ptrSizes, polyOffsets.Count);
                List<int> indices = new List<int>();
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

                int outPolyCount =
                    UnionPolygons(ptr, ptrSizes, portalPolys.Count, outPolyPoints, outptrSizes);
                int[] outPtrSizeArray = new int[outPolyCount];
                Marshal.Copy(outptrSizes, outPtrSizeArray, 0, outPolyCount);
                IntPtr curoutptr = outPolyPoints;
                List<Polygon> polygons = new List<Polygon>();
                int curPtCnt = 0;
                foreach (int ptcnt in outPtrSizeArray)
                {
                    List<Vector2> pts = new List<Vector2>();
                    for (int i = curPtCnt; i < ptcnt; i++)
                    {
                        pts.Add(Marshal.PtrToStructure<Vector2>(curoutptr));
                        curoutptr = IntPtr.Add(curoutptr, v2size);
                    }
                    polygons.Add(new Polygon(pts));
                    curPtCnt = ptcnt;
                }

                Marshal.FreeHGlobal(ptr);
                Marshal.FreeHGlobal(ptrSizes);
                Marshal.FreeHGlobal(outPolyPoints);
                Marshal.FreeHGlobal(outptrSizes);

                return polygons;
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
        }

        public class ConvexMesh
        {
            public System.Numerics.Vector3 color;
            public List<Vector3> points;
        }

        public class Convex
        {
            [DllImport("EdgeIntersect.dll")]
            static extern int ConvexDecomp(IntPtr pointListDbls, int numTriangles,
                IntPtr outPts, int maxPoints, IntPtr outTris, int maxTris);

            [DllImport("kernel32.dll")]
            private static extern bool WriteFile(IntPtr hFile, IntPtr lpBuffer, int NumberOfBytesToWrite, out int lpNumberOfBytesWritten, IntPtr lpOverlapped);

            [DllImport("EdgeIntersect.dll")]
            static extern int LoadCollisionMesh(string name);

            [DllImport("EdgeIntersect.dll")]
            static extern int GetCollisionPartVtxCount(int partidx);

            [DllImport("EdgeIntersect.dll")]
            static extern void GetCollisionPartVertices(int partidx, IntPtr ptr);
            
            [DllImport("EdgeIntersect.dll")]
            static extern void GetCollisionPartBounds(int partidx, IntPtr ptr);
            
            public class Part
            {
                public float[] pts;
                public FL.Vector3 min;
                public FL.Vector3 max;
            }
            public static Part[] LoadCollision(string name)
            {
                int parts = LoadCollisionMesh(name);
                Part[] result = new Part[parts];
                for (int partIdx = 0; partIdx < parts; ++partIdx)
                {
                    int partVtxCount = GetCollisionPartVtxCount(partIdx);
                    IntPtr ptr = Marshal.AllocHGlobal(partVtxCount * sizeof(float) * 3);
                    GetCollisionPartVertices(partIdx, ptr);
                    Part p = new Part();
                    p.pts = new float[partVtxCount * 3];
                    Marshal.Copy(ptr, p.pts, 0, partVtxCount * 3);
                    Marshal.FreeHGlobal(ptr);
                    ptr = Marshal.AllocHGlobal(partVtxCount * sizeof(float) * 6);
                    GetCollisionPartBounds(partIdx, ptr);
                    p.min = Marshal.PtrToStructure<FL.Vector3>(ptr);
                    ptr = IntPtr.Add(ptr, Marshal.SizeOf<FL.Vector3>());
                    p.max = Marshal.PtrToStructure<FL.Vector3>(ptr);
                    Marshal.FreeHGlobal(ptr);
                    result[partIdx] = p;
                }
                return result;
            }
            
            public string writeToFile;

            public List<ConvexMesh> Decomp(List<Vector3> points)
            {
                int v3size = Marshal.SizeOf<Vector3>();
                IntPtr ptr = Marshal.AllocHGlobal(points.Count * v3size);
                IntPtr curptr = ptr;
                int maxPoints = points.Count * 10;
                IntPtr outptr = Marshal.AllocHGlobal(maxPoints * v3size);
                IntPtr outtris = Marshal.AllocHGlobal(points.Count * sizeof(int));
                foreach (var v in points)
                {
                    Marshal.StructureToPtr<Vector3>(v, curptr, false);
                    curptr = IntPtr.Add(curptr, v3size);
                }
                int partCount = ConvexDecomp(ptr, points.Count / 3, outptr, maxPoints,
                    outtris, points.Count);

                List<ConvexMesh> meshes = new List<ConvexMesh>();
                if (partCount > 0)
                {
                    int[] tris = new int[partCount];
                    Marshal.Copy(outtris, tris, 0, partCount);
                    int ptCount = tris[tris.Length - 1];

                    int curPtCnt = 0;
                    IntPtr outPtsCur = outptr;
                    List<byte> outBytes = new List<byte>();
                    outBytes.AddRange(BitConverter.GetBytes(partCount));
                    foreach (var tri in tris)
                    {
                        outBytes.AddRange(BitConverter.GetBytes(tri));
                    }

                    for (int i = 0; i < partCount; i++)
                    {
                        List<Vector3> pts = new List<Vector3>();
                        for (int j = curPtCnt; j < tris[i]; j++)
                        {
                            pts.Add(Marshal.PtrToStructure<Vector3>(outPtsCur));
                            byte[] bytes = new byte[Marshal.SizeOf<Vector3>()];
                            Marshal.Copy(outPtsCur, bytes, 0, bytes.Length);
                            ///outPtsCur
                            outBytes.AddRange(bytes);
                            outPtsCur = IntPtr.Add(outPtsCur, v3size);
                        }
                        curPtCnt = tris[i];
                        meshes.Add(new ConvexMesh() { points = pts });
                    }

                    if (writeToFile != null)
                    {
                        FileStream file = new FileStream(writeToFile, FileMode.Create, FileAccess.Write);
                        file.Write(outBytes.ToArray(), 0, outBytes.Count);
                        file.Close();
                    }
                }
                Marshal.FreeHGlobal(ptr);
                Marshal.FreeHGlobal(outptr);
                Marshal.FreeHGlobal(outtris);                
                return meshes;
            }
        }
    }

    public class LdrLoader
    {
        [DllImport("EdgeIntersect.dll")]
        static extern void LdrLoadFile(IntPtr basepath, IntPtr name, IntPtr matptr);

        [DllImport("EdgeIntersect.dll")]
        static extern void LdrWriteFile(IntPtr basepath, IntPtr name, IntPtr matptr,
            IntPtr outPath, bool force);

        [DllImport("EdgeIntersect.dll")]
        static extern void LdrLoadCachedFile(IntPtr filePath);
        
        [DllImport("EdgeIntersect.dll")]
        static extern IntPtr LdrGetResultPtr();
        [DllImport("EdgeIntersect.dll")]
        static extern int LdrGetResultSize();

        public struct PosTexcoordNrmVertex
        {
            public float m_x;
            public float m_y;
            public float m_z;
            public float m_u;
            public float m_v;
            public float m_nx;
            public float m_ny;
            public float m_nz;
        };
        public void Load(string basePath, string file, System.Numerics.Matrix4x4 matrix, out PosTexcoordNrmVertex[] vertices, 
            out int []indices)
        {
            IntPtr basePathptr = Marshal.StringToHGlobalAnsi(basePath);
            IntPtr fileptr = Marshal.StringToHGlobalAnsi(file);
            IntPtr matptr = Marshal.AllocHGlobal(Marshal.SizeOf<System.Numerics.Matrix4x4>());
            Marshal.StructureToPtr(matrix, matptr, false);
            LdrLoadFile(basePathptr, fileptr, matptr);
            Marshal.FreeHGlobal(basePathptr);
            Marshal.FreeHGlobal(fileptr);
            Marshal.FreeHGlobal(matptr);
            int resultBytes = LdrGetResultSize();
            IntPtr resultData = LdrGetResultPtr();
            int nVertices = Marshal.ReadInt32(resultData);
            resultData = IntPtr.Add(resultData, sizeof(int));
            
            vertices = new PosTexcoordNrmVertex[nVertices];
            int vtxsize = Marshal.SizeOf<PosTexcoordNrmVertex>();
            for (int i = 0; i < nVertices; i++)
            {
                vertices[i] = Marshal.PtrToStructure<PosTexcoordNrmVertex>(resultData);
                resultData = IntPtr.Add(resultData, vtxsize);
            }
            int nIndices = Marshal.ReadInt32(resultData);
            resultData = IntPtr.Add(resultData, sizeof(int));
            indices = new int[nIndices];
            Marshal.Copy(resultData, indices, 0, indices.Length);
        }

        public void LoadCached(string file, out PosTexcoordNrmVertex[] vertices,
            out int[] indices)
        {
            IntPtr basePathptr = Marshal.StringToHGlobalAnsi(file);
            LdrLoadCachedFile(basePathptr);
            Marshal.FreeHGlobal(basePathptr);
            int resultBytes = LdrGetResultSize();
            IntPtr resultData = LdrGetResultPtr();
            int nVertices = Marshal.ReadInt32(resultData);
            resultData = IntPtr.Add(resultData, sizeof(int));

            vertices = new PosTexcoordNrmVertex[nVertices];
            int vtxsize = Marshal.SizeOf<PosTexcoordNrmVertex>();
            for (int i = 0; i < nVertices; i++)
            {
                vertices[i] = Marshal.PtrToStructure<PosTexcoordNrmVertex>(resultData);
                resultData = IntPtr.Add(resultData, vtxsize);
            }
            int nIndices = Marshal.ReadInt32(resultData);
            resultData = IntPtr.Add(resultData, sizeof(int));
            indices = new int[nIndices];
            Marshal.Copy(resultData, indices, 0, indices.Length);
        }

        public void Write(string basePath, string file, System.Numerics.Matrix4x4 matrix,
            string outPath, bool force)
        {
            IntPtr basePathptr = Marshal.StringToHGlobalAnsi(basePath);
            IntPtr fileptr = Marshal.StringToHGlobalAnsi(file);
            IntPtr outPathPtr = Marshal.StringToHGlobalAnsi(outPath);
            IntPtr matptr = Marshal.AllocHGlobal(Marshal.SizeOf<System.Numerics.Matrix4x4>());
            Marshal.StructureToPtr(matrix, matptr, false);
            LdrWriteFile(basePathptr, fileptr, matptr, outPathPtr, force);
            Marshal.FreeHGlobal(basePathptr);
            Marshal.FreeHGlobal(fileptr);
            Marshal.FreeHGlobal(matptr);
            Marshal.FreeHGlobal(outPathPtr);
        }
    }

    public class MbxOrient
    {
        [DllImport("EdgeIntersect.dll")]
        static extern void FindOrientation(IntPtr vertices0, int numvertices0, IntPtr indices0, int numindices0,
                IntPtr vertices1, int numvertices1, IntPtr indices1, int numindices1,
                IntPtr outMatrix);

        public Matrix4x4 Orient(List<Vector3> v0, List<Vector3> v1, List<int> i1)
        {
            var v0arr = v0.Select(v => new System.Numerics.Vector3((float)v.X, (float)v.Y, (float)v.Z)).ToArray();
            int []i0arr = new int[v0arr.Length];
            for (int i = 0; i < i0arr.Length; i++)
            {
                i0arr[i] = (int)i;
            }
            var v1arr = v1.Select(v => new System.Numerics.Vector3((float)v.X, (float)v.Y, (float)v.Z)).ToArray();
            int []i1arr = i1.Select(ii => (int)ii).ToArray();

            int v3size = Marshal.SizeOf<System.Numerics.Vector3>();

            IntPtr v0ptr = Marshal.AllocHGlobal(v0arr.Length * v3size);
            IntPtr curptr0 = v0ptr;
            foreach (System.Numerics.Vector3 v in v0arr)
            {
                Marshal.StructureToPtr(v, curptr0, false);
                curptr0 = IntPtr.Add(curptr0, v3size);
            }

            IntPtr i0ptr = Marshal.AllocHGlobal(i0arr.Length * sizeof(int));
            Marshal.Copy(i0arr, 0, i0ptr, i0arr.Length);

            IntPtr v1ptr = Marshal.AllocHGlobal(v1arr.Length * v3size);
            IntPtr curptr1 = v1ptr;
            foreach (System.Numerics.Vector3 v in v1arr)
            {
                Marshal.StructureToPtr(v, curptr1, false);
                curptr1 = IntPtr.Add(curptr1, v3size);
            }

            IntPtr i1ptr = Marshal.AllocHGlobal(i1arr.Length * sizeof(int));
            Marshal.Copy(i1arr, 0, i1ptr, i1arr.Length);

            IntPtr outMatPtr = Marshal.AllocHGlobal(sizeof(float) * 16);
            FindOrientation(v0ptr, v0arr.Length, i0ptr, i0arr.Length,
                v1ptr, v1arr.Length, i1ptr, i1arr.Length, outMatPtr);


            System.Numerics.Matrix4x4 mat = 
                Marshal.PtrToStructure<System.Numerics.Matrix4x4>(outMatPtr);
            Matrix4x4 dmat = new Matrix4x4(
                (double)mat.M11,
                (double)mat.M12,
                (double)mat.M13,
                (double)mat.M14,

                (double)mat.M21,
                (double)mat.M22,
                (double)mat.M23,
                (double)mat.M24,

                (double)mat.M31,
                (double)mat.M32,
                (double)mat.M33,
                (double)mat.M34,

                (double)mat.M41,
                (double)mat.M42,
                (double)mat.M43,
                (double)mat.M44);

            Marshal.FreeHGlobal(v0ptr);
            Marshal.FreeHGlobal(i0ptr);
            Marshal.FreeHGlobal(v1ptr);
            Marshal.FreeHGlobal(i1ptr);
            Marshal.FreeHGlobal(outMatPtr);

            return dmat;
        }

    }
}
