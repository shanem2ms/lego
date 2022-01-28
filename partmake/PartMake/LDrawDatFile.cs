using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace partmake
{

    public class LDrawDatFile
    {
        List<LDrawDatNode> children = new List<LDrawDatNode>();
        bool hasGeometry = false;
        bool multiColor = false;
        Topology.Mesh topoMesh;

        public List<LDrawDatNode> Children { get => children; }

        Face[] faces;
        public Face[] Faces => faces;
        string name;
        AABB? aabb;

        public string Name { get => name; }
        public bool IsMultiColor { get => multiColor; }
        public LDrawDatFile(string path)
        {
            name = Path.GetFileNameWithoutExtension(path);
            Read(path);
        }

        public AABB GetBBox()
        {
            if (aabb.HasValue)
                return aabb.Value;
            else
            {
                List<Vtx> vertices = new List<Vtx>();
                GetVertices(vertices, false);
                aabb = AABB.CreateFromPoints(vertices.Select(v => v.pos));
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

        public Topology.Mesh GetTopoMesh()
        {
            if (topoMesh == null)
            {
                topoMesh = new Topology.Mesh();
                GetTopoRecursive(false, Matrix4x4.Identity, topoMesh);
                topoMesh.Fix();
            }

            return topoMesh;
        }
        void GetTopoRecursive(bool inverted, Matrix4x4 transform, Topology.Mesh mesh)
        {
            foreach (var child in this.children)
            {
                if (!child.IsEnabled)
                    continue;

                child.File.GetTopoRecursive(inverted ^ child.invert, child.transform * transform,
                    mesh);
            }

            foreach (var f in this.faces)
            {
                List<Vector3> vlist = new List<Vector3>();
                f.GetVertices(vlist, transform, inverted);
                mesh.AddFace(vlist);
            }
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


        public List<Connector> GetConnectors()
        {
            List<Connector> connectors = new List<Connector>();
            List<Connector> rStudCandidates = new List<Connector>();
            GetConnectorsRecursive(connectors, rStudCandidates, false, Matrix4x4.Identity);
            List<Tuple<Vector3, Vector3>> bisectors = new List<Tuple<Vector3, Vector3>>();
            List<Vector3> rStuds = 
                GetTopoMesh().GetRStuds(rStudCandidates.Select(s => Vector3.Transform(Vector3.Zero, s.mat)).ToArray(), bisectors);
            foreach (Vector3 rstud in rStuds)
            {
                connectors.Add(new Connector() { mat = Matrix4x4.CreateScale(4, 4, 4) * 
                    Matrix4x4.CreateTranslation(rstud), type = ConnectorType.RStud });
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
            List<Connector> connectors = GetConnectors();
            if (connectors.Count == 0)
                return;
            string jsonstr = JsonConvert.SerializeObject(connectors);
            string outfile = Path.GetFileNameWithoutExtension(name) + ".json";
            File.WriteAllText(Path.Combine(folder, outfile), jsonstr);
        }

    }
}
