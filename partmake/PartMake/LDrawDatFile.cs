using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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

        Matrix4x4? partMatrix;
        public Matrix4x4 PartMatrix { get { if (partMatrix.HasValue) return partMatrix.Value; return Matrix4x4.Identity; } }

        Connectors connectors = null;
        public Connectors Connectors
        {
            get
            {
                if (connectors == null)
                {
                    connectors = new Connectors();
                }
                return connectors;
            }
            set
            {
                connectors = value;
            }
        }
        Topology.Mesh topoMesh;
        MbxImport.Mesh mbxMesh;
        string description;
        string topoId;
        public List<Topology.Face> TopoFaces => topoMesh?.FacesFromId(topoId);
        public Topology.BSPTree BSPTree => topoMesh?.bSPTree;

        public List<string> ReverseLookup => LDrawFolders.ReverseLookup(Name);
        public List<string> IncludedInParts => LDrawFolders.IncludedInParts(Name);
        public string Name { get => name; }
        public bool IsMultiColor { get => multiColor; }
        public LDrawDatFile(string path)
        {
            name = Path.GetFileNameWithoutExtension(path);
            Read(path);
            //Connectors = new Connectors(this);
            //Connectors.Init(this);
        }

        string MbxPath => Path.Combine(LDrawFolders.MdxFolder, Path.ChangeExtension(this.name, "json"));
        string MbxV2Path => Path.Combine(LDrawFolders.MdxFolder, Path.GetFileNameWithoutExtension(this.name) + "v2.json");

        public MbxImport.Mesh LoadMbx()
        {
            if (mbxMesh == null)
            {
                if (File.Exists(MbxPath))
                    mbxMesh = MbxImport.LoadMeshfile(MbxPath);
                else if (File.Exists(MbxV2Path))
                    mbxMesh = MbxImport.LoadMeshfile(MbxV2Path);
            }
            return mbxMesh;
        }
        LDrawDatFile()
        {
        }
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
            c.connectors = connectors;
            return c;
        }

        public AABB GetBBox()
        {
            if (aabb.HasValue)
                return aabb.Value;
            else
            {
                List<Vtx> vertices = new List<Vtx>();
                GetVertices(vertices, false, false);
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
                    if (this.description == null)
                        this.description = lt.Substring(2);
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

        public event EventHandler<bool> Initialized;
        public bool IsInitialized = false;
        public void AsyncInit()
        {
            Thread t = new Thread(() =>
            {
                RefreshPartMatrix();
                Connectors.Init(this);
                IsInitialized = true;
                Initialized?.Invoke(this, true);
            }
            );
            t.Start();

        }

        void RefreshPartMatrix()
        {
            LoadMbx();
            if (mbxMesh != null)
            {
                List<Vtx> vertices = new List<Vtx>();
                GetVertices(vertices, false, false);
                if (vertices.Count < 10000)
                {
                    var pts = vertices.Select(v => new Vector3(v.pos.X, v.pos.Y, v.pos.Z));
                    MbxOrient mbxOrient = new MbxOrient();
                    Matrix4x4 mat = mbxOrient.Orient(vertices.Select(v => new Vector3(v.pos.X, v.pos.Y, v.pos.Z)).ToList(),
                        mbxMesh.vertices.Select(v => new Vector3(v.X, v.Y, v.Z) * 2.5f).ToList(),
                        mbxMesh.indices);
                    partMatrix = mat;
                }
            }

            if (partMatrix == null)
            {
                AABB aabb = GetBBox();
                partMatrix = Matrix4x4.CreateScale(new Vector3(-1, -1, 1)) * Matrix4x4.CreateTranslation(new Vector3(0, aabb.Max.Y, 0));
            }
        }

        public Topology.Mesh TopoMesh { get => topoMesh; }
        public Topology.Mesh GetTopoMesh()
        {
            if (topoMesh == null)
            {
                topoMesh = new Topology.Mesh();
                GetTopoRecursive(false,
                    PartMatrix,
                    topoMesh, "0");
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

        public int GetFaceCount()
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

        public class SubPartResult
        {
            public LDrawDatNode node;
            public Matrix4x4 mat;
            public bool inverted;
        }
        public List<SubPartResult> GetAllSubPartsOfType(string name)
        {
            List<SubPartResult> results = new List<SubPartResult>();
            GetAllSubPartsOfType(results, name, false, Matrix4x4.Identity);
            return results;
        }

        void GetAllSubPartsOfType(List<SubPartResult> outParts, string name,
            bool inverted, Matrix4x4 transform)
        {
            foreach (var child in this.children)
            {
                Matrix4x4 subMatrix = child.transform * transform;
                if (child.File.name == name)
                    outParts.Add(new SubPartResult
                    {
                        node = child,
                        mat = subMatrix,
                        inverted = inverted ^ child.invert
                    });
                child.File.GetAllSubPartsOfType(outParts, name,
                    inverted ^ child.invert, subMatrix);
            }
        }

        public void SetSubPartSizes()
        {
            SetSubPartSizesRecursive(false, Matrix4x4.Identity);
        }

        void SetSubPartSizesRecursive(bool inverted, Matrix4x4 transform)
        {
            foreach (var child in this.children)
            {
                Matrix4x4 subMatrix = child.transform * transform;
                child.WorldScale = subMatrix.GetScale();
                child.File.SetSubPartSizesRecursive(inverted ^ child.invert, subMatrix);
            }
        }
        public void GetVertices(List<Vtx> vertices, bool onlySelected, bool useMbxMatrix)
        {
            GetVerticesRecursive(false, useMbxMatrix ? PartMatrix : Matrix4x4.Identity, vertices, !onlySelected);
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

        public void GetLDrLoaderMesh(out LdrLoader.PosTexcoordNrmVertex[] ldrvertices,
            out int[] ldrindices)
        {
            LDrawFolders.GetLDrLoader(this.name, PartMatrix.ToM44(), out ldrvertices, out ldrindices);
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

        class Descriptor
        {
            public string type;
            public string subtype;
            public string name;
            public string desc;
            public float[] dims;
            public List<string> aliases;
            public IEnumerable<Connector> Connectors;
        }
        public void WriteDescriptorFile(LDrawFolders.Entry e, string folder, string outname, bool overwrite)
        {
            string outPath = Path.Combine(folder, outname + ".json");
            if (!overwrite && File.Exists(outPath))
                return;
            List<Tuple<Vector3, Vector3>> bisectors = new List<Tuple<Vector3, Vector3>>();
            Descriptor desc = new Descriptor();
            desc.type = e.type;
            desc.subtype = e.subtype;
            desc.name = e.name;
            desc.desc = e.desc;
            desc.dims = e.dims;
            desc.aliases = e.aliases;
            desc.Connectors = Connectors.Items;
            string jsonstr = JsonConvert.SerializeObject(desc);
            File.WriteAllText(outPath, jsonstr);
        }
        public void WriteMeshFile(string outFolder, string outname)
        {
            LDrawFolders.LDrWrite(this.name, PartMatrix.ToM44(),
                Path.Combine(outFolder, outname), false);
        }
        public void WriteCollisionFile(string folder, string outname, bool overwrite)
        {
            try
            {
                string outPath = Path.Combine(folder, outname + ".col");
                if (!overwrite && File.Exists(outPath))
                    return;
                Connectors.DisableConnectors(this);
                Debug.WriteLine(outname);
                Topology.Mesh tm = new Topology.Mesh();
                GetTopoRecursive(false, PartMatrix, tm, "0");
                tm.WriteCollision(outPath);
            }
            catch (Exception ex)
            {

            }
        }
    }
}
