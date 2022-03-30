﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.DoubleNumerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace partmake
{

    public class LDrawDatFile
    {
        List<LDrawDatNode> children = new List<LDrawDatNode>();
        bool hasGeometry = false;
        bool multiColor = false;

        public List<LDrawDatNode> Children { get => children; }

        Face[] faces;
        public Face[] Faces => faces;
        string name;
        AABB? aabb;

        Topology.Mesh topoMesh;
        string topoId;
        public List<Topology.Face> TopoFaces => topoMesh?.FacesFromId(topoId);
        public Topology.BSPTree BSPTree => topoMesh?.bSPTree;

        public string Name { get => name; }
        public bool IsMultiColor { get => multiColor; }
        public LDrawDatFile(string path)
        {
            name = Path.GetFileNameWithoutExtension(path);
            Read(path);
        }

        LDrawDatFile()
        { }
        public LDrawDatFile Clone()
        {
            LDrawDatFile c = new LDrawDatFile();
            c.name = name;
            
            foreach (LDrawDatNode node in children)
            {
                c.children.Add(node.Clone());
            }

            c.faces = faces;
            c.aabb = aabb;
            c.hasGeometry = hasGeometry;
            c.multiColor = multiColor;
            return c;
        }

        public AABB GetBBox()
        {
            if (aabb.HasValue)
                return aabb.Value;
            else
            {
                List<Vtx> vertices = new List<Vtx>();
                GetVertices(vertices, false);
                aabb = AABB.CreateFromPoints(vertices.Select(v => new Vector3(v.pos.X, v.pos.Y, v.pos.Z)));
                return aabb.Value;
            }
        }
        void Read(string path)
        {
            string[] lines = File.ReadAllLines(path);
            List<Face> fl = new List<Face>();

            bool invertnext = false;
            foreach (string line in lines)
            {
                string lt = line.Trim();
                if (lt.Length == 0)
                    continue;
                if (lt[0] == '0')
                {
                    if (lt.Trim() == @"0 BFC INVERTNEXT")
                    {
                        invertnext = true;
                    }
                }
                if (lt[0] == '1')
                {
                    Regex r = new Regex(@"1\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\w\\]+\.dat)$");
                    Match m = r.Match(lt);
                    if (m.Groups.Count >= 15)
                    {
                        string nextfile = m.Groups[14].Value;
                        //Debug.WriteLine(nextfile);

                        int colIdx = int.Parse(m.Groups[1].Value);
                        multiColor |= (colIdx != 16);
                        Matrix4x4 mat = new Matrix4x4();
                        mat = Matrix4x4.Identity;
                        mat.M41 = float.Parse(m.Groups[2].Value);
                        mat.M42 = float.Parse(m.Groups[3].Value);
                        mat.M43 = float.Parse(m.Groups[4].Value);
                        mat.M11 = float.Parse(m.Groups[5].Value);
                        mat.M21 = float.Parse(m.Groups[6].Value);
                        mat.M31 = float.Parse(m.Groups[7].Value);
                        mat.M12 = float.Parse(m.Groups[8].Value);
                        mat.M22 = float.Parse(m.Groups[9].Value);
                        mat.M32 = float.Parse(m.Groups[10].Value);
                        mat.M13 = float.Parse(m.Groups[11].Value);
                        mat.M23 = float.Parse(m.Groups[12].Value);
                        mat.M33 = float.Parse(m.Groups[13].Value);

                        LDrawDatFile d = LDrawFolders.GetPart(nextfile);
                        if (d != null)
                        {
                            hasGeometry |= d.hasGeometry;
                            multiColor |= d.IsMultiColor;
                            if (d.hasGeometry)
                                this.children.Add(new LDrawDatNode() { File = d, invert = invertnext, transform = mat });
                        }
                        else
                            Debug.WriteLine(string.Format("Cant find {0}", nextfile));
                    }
                    invertnext = false;
                    //1 16 0 4 4 -6 0 0 0 -1 0 0 0 6 rect.dat
                }
                else if (lt[0] == '3')
                {
                    Regex r = new Regex(@"3\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)$");
                    Match m = r.Match(lt);
                    if (m.Groups.Count >= 11)
                    {
                        int colIdx = int.Parse(m.Groups[1].Value);
                        multiColor |= (colIdx != 16);
                        Face f = new Face(3);
                        for (int i = 0; i < 3; ++i)
                        {
                            f.v[i] = new Vector3(float.Parse(m.Groups[i * 3 + 2].Value),
                                float.Parse(m.Groups[i * 3 + 3].Value),
                                float.Parse(m.Groups[i * 3 + 4].Value));
                        }
                        f.CheckCoPlanar();
                        fl.Add(f);
                        hasGeometry = true;
                    }
                }
                else if (lt[0] == '4')
                {
                    Regex r = new Regex(@"4\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)$");
                    Match m = r.Match(lt);
                    if (m.Groups.Count >= 14)
                    {
                        int colIdx = int.Parse(m.Groups[1].Value);
                        multiColor |= (colIdx != 16);
                        Face f = new Face(4);
                        for (int i = 0; i < 4; ++i)
                        {
                            f.v[i] = new Vector3(float.Parse(m.Groups[i * 3 + 2].Value),
                                float.Parse(m.Groups[i * 3 + 3].Value),
                                float.Parse(m.Groups[i * 3 + 4].Value));
                        }
                        //f.CheckCoPlanar();
                        fl.Add(f);
                        hasGeometry = true;
                    }
                }
            }

            this.faces = fl.ToArray();
        }

        public void ClearTopoMesh()
        {
            topoMesh = null;
        }
        public Topology.Mesh GetTopoMesh()
        {
            if (topoMesh == null)
            {
                topoMesh = new Topology.Mesh();
                GetTopoRecursive(false, Matrix4x4.Identity, topoMesh, "0");
                topoMesh.Fix();
            }

            return topoMesh;
        }
        void GetTopoRecursive(bool inverted, Matrix4x4 transform, Topology.Mesh mesh, string id)
        {
            int childIdx = 0;
            foreach (var child in this.children)
            {
                if (child.IsEnabled)
                {
                    child.File.GetTopoRecursive(inverted ^ child.invert, child.transform * transform,
                        mesh, id + "." + childIdx.ToString());
                }
                childIdx++;
            }

            if (this.faces.Count() > 0)
            {
                this.topoMesh = mesh;
                this.topoId = id;
                foreach (var f in this.faces)
                {
                    List<Vector3> vlist = new List<Vector3>();
                    f.GetVertices(vlist, transform, inverted);
                    mesh.AddFace(id, vlist);
                }
            }
        }

        int GetFaceCount()
        {
            int faceCount = 0;
            foreach (var child in this.children)
            {
                if (child.IsEnabled)
                {
                    faceCount += child.File.GetFaceCount();
                }
            }
            faceCount += this.faces.Count();
            return faceCount;
        }
        public void GetVertices(List<Vtx> vertices, bool onlySelected)
        {
            GetVerticesRecursive(false, Matrix4x4.Identity, vertices, !onlySelected);
        }

        Vector3 V3T(Vector3 v, Matrix4x4 m)
        {
            Vector4 v4 = Vector4.Transform(new Vector4(v, 1), m);
            return new Vector3(v4.X, v4.Y, v4.Z);
        }
        void GetVerticesRecursive(bool inverted, Matrix4x4 transform, List<Vtx> vertices, bool enabled)
        {
            foreach (var child in this.children)
            {
                if (!child.IsEnabled)
                    continue;
                child.File.GetVerticesRecursive(inverted ^ child.invert, child.transform * transform,
                    vertices, enabled | child.IsSelected);
            }

            if (enabled)
            {
                foreach (var f in this.faces)
                {
                    if (!f.IsEnabled)
                        continue;
                    f.GetTriangleVertices(vertices, transform, inverted);
                }
            }
        }

        public enum ConnectorType
        {
            Stud = 1,
            Clip = 2,
            StudJ = 4,
            RStud = 8
        }
        public class Connector : IComparable<Connector>
        {
            public ConnectorType type;
            public Matrix4x4 mat;

            public int CompareTo(Connector other)
            {
                int i = type.CompareTo(other.type);
                if (i != 0)
                    return i;
                return mat.GetHashCode().CompareTo(other.mat.GetHashCode());
            }
        }

        static HashSet<string> studs = new HashSet<string>() { "stud", "stud9", "stud10", "stud15", "studel", "studp01" };
        static HashSet<string> studclipj = new HashSet<string>() { "stud2", "stud6", "stud6a", "stud2a" };
        static HashSet<string> rstud = new HashSet<string>() { "stud4", "stud4a", "stud4o", "stud4f1s",
                "stud4f1w", "stud4f2n","stud4f2s", "stud4f2w", "stud4f3s", "stud4f4s", "stud4f4n", "stud4f5n" };


        public void DisableConnectors()
        {
            DisableConnectorsRecursive();
        }
        public List<Connector> GetConnectors(ref List<Tuple<Vector3, Vector3>> bisectors)
        {
            List<Connector> connectors = new List<Connector>();
            List<Connector> rStudCandidates = new List<Connector>();
            GetConnectorsRecursive(connectors, rStudCandidates, false, Matrix4x4.Identity);
            var rStuds = 
                GetTopoMesh().GetRStuds(rStudCandidates.Select(s => Vector3.Transform(Vector3.Zero, s.mat)).ToArray(), bisectors);
            foreach (var rstud in rStuds)
            {
                Vector3 u = rstud.Item2.udir;
                Vector3 n = rstud.Item2.normal;
                Vector3 v = rstud.Item2.vdir;
                Matrix4x4 m = new Matrix4x4(
                    v.X, v.Y, v.Z, 0,
                    n.X, n.Y, n.Z, 0,
                    u.X, u.Y, u.Z, 0,
                    0, 0, 0, 1);

                //rstud.Item2.
                connectors.Add(new Connector() { mat = Matrix4x4.CreateScale(4, 4, 4) * m *
                    Matrix4x4.CreateTranslation(rstud.Item1), type = ConnectorType.RStud });
            }
            return connectors.Distinct().ToList();
        }

        Connector CreateBaseConnector(Matrix4x4 mat, ConnectorType type)
        {
            AABB bb = GetBBox();
            Vector3 scl = bb.Max - bb.Min;
            Vector3 off = (bb.Max + bb.Min) * 0.5f;
            Matrix4x4 cm = Matrix4x4.CreateScale(scl) *
                Matrix4x4.CreateTranslation(off) * mat;
            return new Connector() { mat = cm, type = type };
        }

        public void GetPrimitives(List<Primitive> primitives)
        {
            GetPrimitivesRecursive(primitives, false, Matrix4x4.Identity);
        }
        
        static Dictionary<string, PrimitiveType> sPrimTypeMap = new Dictionary<string, PrimitiveType>()
        { { "4-4cyli", PrimitiveType.Cylinder } };

        void GetPrimitivesRecursive(List<Primitive> primitives, bool inverted, Matrix4x4 transform)
        {
            PrimitiveType ptype;
            if (sPrimTypeMap.TryGetValue(this.Name, out ptype))
            {
                primitives.Add(new Primitive() { type = ptype, transform = transform, inverted = inverted });
            }
            foreach (var child in this.children)
            {
                if (!child.IsEnabled)
                    continue;

                child.File.GetPrimitivesRecursive(primitives, inverted ^ child.invert, child.transform * transform);
            }
        }

        bool DisableConnectorsRecursive()
        {
            if (studs.Contains(this.name) ||
                studclipj.Contains(this.name) ||
                rstud.Contains(this.name) ||
                this.name == "stud12" ||
                this.name == "stud3" || this.name == "stud3a" ||
                this.name == "stud4h" || this.name == "stud18a")
            {
                return true;
            }
            else
            {
                foreach (var child in this.children)
                {
                    if (child.File.DisableConnectorsRecursive())
                        child.IsEnabled = false;
                }
                return false;
            }
        }
        void GetConnectorsRecursive(List<Connector> connectors, List<Connector> rStudCandidates, bool inverted, Matrix4x4 transform)
        {
            if (studs.Contains(this.name))
            {
                AABB aabb = GetBBox();
                connectors.Add(CreateBaseConnector(Matrix4x4.CreateTranslation(0, 2, 0) * transform, ConnectorType.Stud));
            }
            else if (studclipj.Contains(this.name))
            {
                connectors.Add(CreateBaseConnector(Matrix4x4.CreateTranslation(0, 2, 0) * transform, 
                    ConnectorType.Stud | ConnectorType.StudJ | ConnectorType.Clip));
            }
            else if (rstud.Contains(this.name))
            {
                Vector3[] offsets = new Vector3[]
                {
                    new Vector3(0, 0, 0),
                    new Vector3(10, 0, 10),
                    new Vector3(-10, 0, 10),
                    new Vector3(10, 0, -10),
                    new Vector3(-10, 0, -10),
                };
                for (int i = 0; i < offsets.Length; ++i)
                {
                    rStudCandidates.Add(CreateBaseConnector(Matrix4x4.CreateTranslation(offsets[i]) * transform, 
                        ConnectorType.RStud));
                }
            }
            else if (this.name == "stud12")
            {
                Vector3[] offsets = new Vector3[]
                {
                    new Vector3(10, 0, 10),
                    new Vector3(-10, 0, 10),
                    new Vector3(10, 0, -10),
                    new Vector3(-10, 0, -10),
                };
                for (int i = 0; i < offsets.Length; ++i)
                {
                    rStudCandidates.Add(CreateBaseConnector(Matrix4x4.CreateTranslation(offsets[i]) * transform,
                        ConnectorType.RStud));
                }
            }
            else if (this.name == "stud3" || this.name == "stud3a")
            {
                Vector3[] offsets = new Vector3[]
                               {
                    new Vector3(10, 0, 0),
                    new Vector3(-10, 0, 0),
                               };
                for (int i = 0; i < offsets.Length; ++i)
                {
                    rStudCandidates.Add(CreateBaseConnector(Matrix4x4.CreateTranslation(offsets[i]) * transform,
                        ConnectorType.RStud));
                }
            }
            else if (this.name == "stud4h" || this.name == "stud18a")
            {
                Vector3[] offsets = new Vector3[]
                               {
                    new Vector3(0, 0, 0),
                    new Vector3(-10, 0, 0),
                    new Vector3(10, 0, 0),
                               };
                for (int i = 0; i < offsets.Length; ++i)
                {
                    rStudCandidates.Add(CreateBaseConnector(Matrix4x4.CreateTranslation(offsets[i]) * transform,
                        ConnectorType.RStud));
                }
            }
            else
            {
                foreach (var child in this.children)
                {
                    child.File.GetConnectorsRecursive(connectors, rStudCandidates, inverted ^ child.invert, child.transform * transform);
                }
            }
        }

        public void WriteConnectorFile(string folder)
        {
            if (GetFaceCount() > 1000)
                return;
            List<Tuple<Vector3, Vector3>> bisectors = new List<Tuple<Vector3, Vector3>>();
            List<Connector> connectors = GetConnectors(ref bisectors);
            if (connectors == null || connectors.Count == 0)
                return;
            string jsonstr = JsonConvert.SerializeObject(connectors);
            string outfile = Path.GetFileNameWithoutExtension(name) + ".json";
            File.WriteAllText(Path.Combine(folder, outfile), jsonstr);
        }

        public void WriteCollisionFile(string folder)
        {
            try
            {
                DisableConnectors();
                Debug.WriteLine(name);
                Topology.Mesh tm = new Topology.Mesh();
                GetTopoRecursive(false, Matrix4x4.Identity, tm, "0");
                string outfile = Path.GetFileNameWithoutExtension(name) + ".col";
                tm.WriteCollision(Path.Combine(folder, outfile));
            }
            catch (Exception ex)
            {

            }
        }
    }
}
