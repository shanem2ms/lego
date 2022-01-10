using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Numerics;

namespace partmake
{
    
    public class LDrawDatFile
    {                
        List<LDrawDatNode> children = new List<LDrawDatNode>();
        bool hasGeometry = false;

        public List<LDrawDatNode> Children { get => children; }

        Face[] faces;
        string name;

        public string Name { get => name; }
        public LDrawDatFile(string path)
        {
            name = Path.GetFileNameWithoutExtension(path);
            Read(path);
        }
        void Read(string path)
        {
            string []lines = File.ReadAllLines(path);
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
                            if (d.hasGeometry)
                                this.children.Add(new LDrawDatNode() { File = d, invert = invertnext, transform = mat });
                        }
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
                        Face f = new Face(4);
                        for (int i = 0; i < 4; ++i)
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
            }

            this.faces = fl.ToArray();
        }

        public void GetTopoMesh(Topology.Mesh mesh)
        {
            GetTopoRecursive(false, Matrix4x4.Identity, mesh);
        }
        void GetTopoRecursive(bool inverted, Matrix4x4 transform, Topology.Mesh mesh)
        {
            foreach (var child in this.children)
            {
                child.File.GetTopoRecursive(inverted ^ child.invert, child.transform * transform,
                    mesh);
            }

            foreach (var f in this.faces)
            {
                mesh.AddFace(f.v);
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
                child.File.GetVerticesRecursive(inverted ^ child.invert, child.transform * transform,
                    vertices, enabled | child.IsSelected);
            }

            if (enabled)
            {
                foreach (var f in this.faces)
                {
                    f.GetVertices(vertices, transform, inverted);
                }
            }
        }
    }
}
