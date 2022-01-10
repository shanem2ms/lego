using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace partmake
{
    namespace Topology
    {
        public class Vertex
        {
            public int idx;
            public Vector3 pt;
            public List<Edge> edges =
                new List<Edge>();
        }

        public class Loop
        {
            public List<Edge> edges;

            public void GetVertices(List<Vtx> vertices)
            {
                Vector3 anchor = edges[0].v0.pt;
                Vector3 nrm = Vector3.Normalize(Vector3.Cross(edges[0].dir, edges[1].dir));
                for (int i = 1; i < edges.Count-1; ++i)
                {
                    vertices.Add(new Vtx(anchor, nrm, new Vector2(0, 0)));
                    vertices.Add(new Vtx(edges[i].v0.pt, nrm, new Vector2(0, 0)));
                    vertices.Add(new Vtx(edges[i].v1.pt, nrm, new Vector2(0, 0)));
            }
            }
        }

        public class Edge
        {
            public Edge(Vertex _v0, Vertex _v1)
            {
                v0 = _v0;
                v1 = _v1;
                dir = Vector3.Normalize(v1.pt - v0.pt);
                len = (v1.pt - v0.pt).Length();
            }
            public Vertex v0;
            public Vertex v1;
            public Vector3 dir;
            public float len;
            public bool found;
            public bool instack = false;            
        }

        public class Face
        {
            public List<Edge> edges;

            void GetVertices(List<Vertex> v)
            {
                HashSet<int> vidxSet = new HashSet<int>();
                
                foreach (Edge e in edges)
                {
                    if (!vidxSet.Contains(e.v0.idx))
                    {
                        v.Add(e.v0);
                        vidxSet.Add(e.v0.idx);
                    }
                    if (!vidxSet.Contains(e.v1.idx))
                    {
                        v.Add(e.v1);
                        vidxSet.Add(e.v1.idx);
                    }
                }
            }

            public ulong ComputeHash()
            {
                List<Vertex> vlist = new List<Vertex>();
                GetVertices(vlist);
                vlist.Sort((a, b) => a.idx.CompareTo(b.idx));
                UInt64 hashedValue = 3074457345618258791ul;
                foreach (Vertex v in vlist)
                {
                    hashedValue += (ulong)v.idx;
                    hashedValue *= 3074457345618258799ul;
                }
                return hashedValue;
            }
        }


        public class Mesh
        {
            public Dictionary<Vector3, Vertex> vertexDict
                = new Dictionary<Vector3, Vertex>();
            public List<Face> faces = new List<Face>();
            int nextVtxIdx = 0;
            public void AddFace(Vector3[] vertices)
            {
                List<Vertex> verlist = new List<Vertex>();
                foreach (var v in vertices)
                {
                    if (vertexDict.ContainsKey(v))
                    {
                        verlist.Add(vertexDict[v]);
                    }
                    else
                    {
                        Vertex nv = new Vertex() { pt = v, idx = nextVtxIdx++};
                        verlist.Add(nv);
                        vertexDict.Add(v, nv);
                    }
                }
                List<Edge> elist = new List<Edge>();
                for (int idx = 0; idx < verlist.Count; ++idx)
                {
                    Edge e =
                        new Edge(verlist[idx], verlist[(idx + 1) % verlist.Count]);
                    e.v0.edges.Add(e);
                    e.v1.edges.Add(e);
                    elist.Add(e);
                }

                Face f = new Face() { edges = elist };
                this.faces.Add(f);
            }


            bool FollowEdge(List<Edge> edges)
            {
                Edge cur = edges.Last();
                if (cur.instack)
                {
                    if (edges[0] == cur && edges.Count() == 5)
                        return true;
                    else
                        return false;
                }
                cur.instack = true;
                foreach (Edge e in cur.v1.edges)
                {
                    if (!e.found && 
                        e.len == 12 && Vector3.Dot(cur.dir, e.dir) == 0)
                    {
                        edges.Add(e);
                        if (FollowEdge(edges))
                        {
                            cur.instack = false;
                            return true;
                        }
                        edges.Remove(e);
                    }
                }

                cur.instack = false;
                return false;
            }

            public void RemoveDuplicateFaces()
            {
                Dictionary<ulong, Face> faceHashes = new Dictionary<ulong, Face>();
                for (int idx = 0; idx < faces.Count; )
                {
                    Face f = faces[idx];
                    ulong faceHash = f.ComputeHash();
                    if (faceHashes.ContainsKey(faceHash))
                    {
                        faces.RemoveAt(idx);
                    }
                    else
                    {
                        faceHashes.Add(faceHash, f);
                        ++idx;
                    }
                }
            }
            void ClearFound()
            {
                foreach (Face f in faces)
                {
                    foreach (Edge e in f.edges)
                    {
                        e.found = false;
                    }
                }
            }

            public List<Loop> FindSquares()
            {
                List<Loop> foundLoops = new List<Loop>();
                foreach (Face f in faces)
                {
                    foreach (Edge e in f.edges)
                    {
                        if (!e.found && e.len == 12)
                        {
                            List<Edge> edges = new List<Edge>() { e };
                            if (FollowEdge(edges))
                            {
                                foreach (Edge fe in edges)
                                {
                                    fe.found = true;
                                }
                                foundLoops.Add(new Loop() { edges = edges });
                            }
                        }
                    }
                }

                return foundLoops;
            }
        }
    }
}