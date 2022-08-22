using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.DoubleNumerics;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Linq;

namespace partmake
{
    public enum ConnectorType
    {
        Stud = 1,
        Clip = 2,
        StudJ = 3,
        RStud = 4,
        MFigHipLeg = 5,
        MFigRHipLeg = 6,
        MFigHipStud = 7,
        MFigTorsoRArm = 8,
        MFigTorsoNeck = 9,
        MFigHeadRNeck = 10,
        MFigArmKnob = 11,
        MFigRWrist = 12,
        MFigWrist = 13,
        MFigRHandGrip = 14,
        MFigRHipStud = 15,
        Stem = 16,
        StemHole = 17
    }

    public static class Vector3Parse
    {
        public static string Str(this Vector3 v)
        {
            return $"{v.X}, {v.Y}, {v.Z}";
        }

        public static Vector3 Parse(string str)
        {
            try
            {
                string[] vals = str.Split(',');
                if (vals.Length == 3)
                {
                    Vector3 v = new Vector3(double.Parse(vals[0]),
                        double.Parse(vals[1]),
                        double.Parse(vals[2]));
                    return v;
                }
            }
            catch
            {
            }
            return Vector3.Zero;
        }

        public static Quaternion FromVecs(Vector3 xdir, Vector3 ydir)
        {
            ydir = Vector3.Normalize(ydir);
            xdir = Vector3.Normalize(xdir);
            Vector3 zdir = Vector3.Cross(xdir, ydir);
            xdir = Vector3.Cross(ydir, zdir);
            Matrix4x4 m = new Matrix4x4(xdir.X, xdir.Y, xdir.Z, 0,
                ydir.X, ydir.Y, ydir.Z, 0,
                zdir.X, zdir.Y, zdir.Z, 0,
                0, 0, 0, 1);
            return Quaternion.CreateFromRotationMatrix(m);
        }
        public static Quaternion ParseQuat(string str)
        {
            try
            {
                string[] vals = str.Split('[');
                if (vals.Length == 3)
                {
                    Vector3 ydir = Parse(vals[1].Trim(new char[] { ' ', ']' }));
                    Vector3 xdir = Parse(vals[2].Trim(new char[] { ' ', ']' }));
                    return FromVecs(xdir, ydir);
                }
            }
            catch { }
            return Quaternion.Identity;
        }
        public static string Str(this Quaternion q)
        {
            Vector3 ydir = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, q));
            Vector3 xdir = Vector3.Normalize(Vector3.Transform(Vector3.UnitX, q));
            return $"[{ydir.Str()}] [{xdir.Str()}]";
        }

        public static void Decompose(this Matrix4x4 mat, out Vector3 translate,
            out Vector3 scale, out Quaternion rotation)
        {
            translate = mat.Translation;
            scale = mat.GetScale();
            rotation = Quaternion.CreateFromRotationMatrix(mat);
        }

        public static Matrix4x4 Compose(Vector3 translate,
            Vector3 scale, Quaternion rotation)
        {
            return Matrix4x4.CreateScale(scale) *
                Matrix4x4.CreateFromQuaternion(rotation) *
                Matrix4x4.CreateTranslation(translate);
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class Connector : IComparable<Connector>
    {
        ConnectorType type;
        [JsonProperty]
        public ConnectorType Type { get => type; set { type = value; Changed?.Invoke(this, true); } }

        [JsonProperty]
        public Matrix4x4 Mat { get; set; }

        System.Numerics.Matrix4x4? im44;
        System.Numerics.Matrix4x4? m44;
        public System.Numerics.Matrix4x4 IM44
        {
            get
            {
                if (!im44.HasValue)
                {
                    System.Numerics.Matrix4x4 outmat;
                    System.Numerics.Matrix4x4.Invert(M44, out outmat); im44 = outmat;
                }
                return im44.Value;
            }
        }
        public System.Numerics.Matrix4x4 M44
        {
            get
            {
                if (!m44.HasValue)
                {
                    m44 = Mat.ToM44();
                }
                return m44.Value;
            }
        }

        [JsonProperty]
        public bool IsCustom { get; set; } = false;

        public string Offset
        {
            get => Mat.Translation.Str();
            set
            {
                Vector3 v = Vector3Parse.Parse(value);
                Matrix4x4 m = Mat; m.Translation = v; Mat = m;
                Changed?.Invoke(this, true);
            }
        }

        public string Rotation
        {
            get
            {
                Vector4 v = Vector4.Transform(new Vector4(0, 1, 0, 0), Mat);
                Matrix4x4 m = Mat;
                Matrix4x4.Invert(m, out m);
                m = Matrix4x4.Transpose(m);
                Vector3 vb = Vector3.Normalize(Vector3.TransformNormal(new Vector3(0,1,0), m));
                Vector3 v3 = Vector3.Normalize(new Vector3(v.X, v.Y, v.Z));
                return Quaternion.CreateFromRotationMatrix(Mat).Str();
            }
            set
            {
                Vector3 t, s;
                Quaternion q;
                Mat.Decompose(out t, out s, out q);
                Quaternion newq = Vector3Parse.ParseQuat(value);
                Mat = Vector3Parse.Compose(t, s, newq);
                Changed?.Invoke(this, true);
            }
        }

        public Vector3 Scale => Mat.GetScale();

        public bool IsSelected = false;
        public static string[] ConnectorTypes => Enum.GetNames(typeof(ConnectorType));
        public override string ToString()
        {
            return $"{Type} {Mat.Translation}";
        }
        public int CompareTo(Connector other)
        {
            int i = Type.CompareTo(other.Type);
            if (i != 0)
                return i;
            return Mat.GetHashCode().CompareTo(other.Mat.GetHashCode());
        }

        public event EventHandler<bool> Changed;
    }

    public class Connectors
    {

        List<Tuple<Vector3, Vector3>> bisectors = null;
        ObservableCollection<Connector> connectors = new ObservableCollection<Connector>();

        public ObservableCollection<Connector> Items => connectors;

        public List<Tuple<Vector3, Vector3>> bisectorsOverride = null;
        public List<Tuple<Vector3, Vector3>> BisectorsOverride { set => bisectorsOverride = value; }
        public List<Tuple<Vector3, Vector3>> Bisectors
        {
            get
            {
                if (bisectorsOverride != null) return bisectorsOverride;
                else
                    return bisectors;
            }
        }

        static HashSet<string> studs = new HashSet<string>() { "stud", "stud9", "stud10", "stud15", "studel", "studp01" };
        static HashSet<string> studclipj = new HashSet<string>() { "stud2", "stud6", "stud6a", "stud2a" };
        static HashSet<string> rstud = new HashSet<string>() { "stud4", "stud4a", "stud4o", "stud4f1s",
                "stud4f1w", "stud4f2n","stud4f2s", "stud4f2w", "stud4f3s", "stud4f4s", "stud4f4n", "stud4f5n" };

        string partname;

        //partmake\connectors
        public Connectors()
        {

        }
        public void Init(LDrawDatFile thisfile)
        {
            this.partname = thisfile.Name;
            List<Connector> connectors = new List<Connector>();
            List<Connector> rStudCandidates = new List<Connector>();
            GetConnectorsRecursive(thisfile, connectors, rStudCandidates, false, thisfile.PartMatrix);
            this.bisectors = new List<Tuple<Vector3, Vector3>>();
            var rStuds =
                Topology.ConnectorUtils.GetRStuds(thisfile.GetTopoMesh(), rStudCandidates.Select(s => Vector3.Transform(Vector3.Zero, s.Mat)).ToArray(),
                this.bisectors);
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

                connectors.Add(new Connector()
                {
                    Mat = Matrix4x4.CreateScale(12, -4, 12) * m *
                    Matrix4x4.CreateTranslation(rstud.Item1),
                    Type = ConnectorType.RStud
                });
            }
            //Topology.ConnectorUtils.FindLoops(GetTopoMesh());
            this.connectors = new ObservableCollection<Connector>(connectors.Distinct().ToList());
            LoadConnectorFile();
        }
        Connector CreateBaseConnector(LDrawDatFile file, Matrix4x4 mat, ConnectorType type)
        {
            return CreateBaseConnector(file, mat, Matrix4x4.Identity, type);
        }
        Connector CreateBaseConnector(LDrawDatFile file, Matrix4x4 mat, Matrix4x4 rot, ConnectorType type)
        {
            AABB bb = file.GetBBox();
            Vector3 scl = bb.Max - bb.Min;
            Vector3 off = (bb.Max + bb.Min) * 0.5f;
            Matrix4x4 cm = Matrix4x4.CreateScale(scl) *
                rot *
                Matrix4x4.CreateTranslation(off) * mat;
            return new Connector() { Mat = cm, Type = type };
        }

        void GetConnectorsRecursive(LDrawDatFile file, List<Connector> connectors, List<Connector> rStudCandidates, bool inverted, Matrix4x4 transform)
        {
            if (studs.Contains(file.Name))
            {
                AABB aabb = file.GetBBox();
                connectors.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(0, 2, 0) * transform, ConnectorType.Stud));
            }
            else if (studclipj.Contains(file.Name))
            {
                connectors.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(0, 2, 0) * transform,
                    ConnectorType.Stud));
                connectors.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(0, 2, 0) * transform,
                    ConnectorType.StudJ));
                connectors.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(0, 2, 0) * transform,
                    ConnectorType.Clip));
            }
            else if (rstud.Contains(file.Name))
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
                    rStudCandidates.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(offsets[i]) * transform,
                        ConnectorType.RStud));
                }
            }
            else if (file.Name == "stud12")
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
                    rStudCandidates.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(offsets[i]) * transform,
                        ConnectorType.RStud));
                }
            }
            else if (file.Name == "stud3" || file.Name == "stud3a")
            {
                Vector3[] offsets = new Vector3[]
                               {
                    new Vector3(10, 0, 0),
                    new Vector3(-10, 0, 0),
                               };
                for (int i = 0; i < offsets.Length; ++i)
                {
                    rStudCandidates.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(offsets[i]) * transform,
                        ConnectorType.RStud));
                }
            }
            else if (file.Name == "stud4h" || file.Name == "stud18a")
            {
                Vector3[] offsets = new Vector3[]
                               {
                    new Vector3(0, 0, 0),
                    new Vector3(-10, 0, 0),
                    new Vector3(10, 0, 0),
                               };
                for (int i = 0; i < offsets.Length; ++i)
                {
                    rStudCandidates.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(offsets[i]) * transform,
                        ConnectorType.RStud));
                }
            }
            else if (file.Name == "4-4cylc")
            {
                Vector3 scale = transform.GetScale();
                if (Eps.Eq2(scale.X, 3) &&
                    Eps.Eq2(scale.Z, 3))
                {
                    connectors.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(0, -0.1, 0) * transform,
                        ConnectorType.MFigHipLeg));
                    connectors.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(0, -1.1, 0) *
                        Matrix4x4.CreateFromAxisAngle(Vector3.UnitX, Math.PI) *
                        transform,
                        ConnectorType.MFigHipLeg));
                }
            }
            else if (file.Name == "3-4cyli")
            {
                Vector3 scale = transform.GetScale();
                if (Eps.Eq2(scale.X, 6) &&
                    Eps.Eq2(scale.Z, 6))
                {
                    connectors.Add(CreateBaseConnector(file,
                        Matrix4x4.CreateFromAxisAngle(Vector3.UnitX, Math.PI) *
                        Matrix4x4.CreateTranslation(0, 0.5, 0) *
                        transform,
                        ConnectorType.MFigTorsoNeck));
                }
            }
            else if ((file.Name == "4-4cyli" ||
                file.Name == "4-4cylo"))
            {
                Vector3 scale = transform.GetScale();
                if (inverted)
                {
                    if (Eps.Eq2(scale.X, 3) &&
                        Eps.Eq2(scale.Z, 3))
                    {
                        connectors.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(0, 0.5, 0) * transform,
                            ConnectorType.MFigRHipLeg));
                    }
                    if (Eps.Eq2(scale.X, 5) &&
                        Eps.Eq2(scale.Z, 5))
                    {
                        connectors.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(0, 0.5, 0) * transform,
                            ConnectorType.MFigTorsoRArm));
                    }
                    if (Eps.Eq2(scale.X, 6) &&
                        Eps.Eq2(scale.Z, 6))
                    {
                        connectors.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(0, 0.5, 0) * transform,
                            ConnectorType.MFigHeadRNeck));
                    }
                    if (Eps.Eq2(scale.X, 2) &&
                        Eps.Eq2(scale.Z, 2))
                    {
                        connectors.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(0, 0.5, 0) * transform,
                            ConnectorType.StemHole));
                    }
                }
                else
                {
                    if (Eps.Eq2(scale.X, 2) &&
                        Eps.Eq2(scale.Z, 2))
                    {
                        connectors.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(0, 0.0, 0) * transform,
                            ConnectorType.Stem));
                    }
                    if (Eps.Eq2(scale.X, 2) &&
                        Eps.Eq2(scale.Z, 2.236))
                    {
                        connectors.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(0, 0.0, 0) * transform,
                            ConnectorType.Stem));
                    }
                }
            }
            else if (file.Name == "knob1")
            {
                connectors.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(-2, 0, 0) * 
                    transform, Matrix4x4.CreateRotationZ(Math.PI / 2.0), 
                    ConnectorType.MFigArmKnob));
            }
            else if (file.Name == "hipstud")
            {
                Vector3 scale = transform.GetScale();
                {
                    connectors.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(0, -5, 0) * transform,
                        ConnectorType.MFigHipStud));
                }
            }

            else
            {
                foreach (var child in file.Children)
                {
                    GetConnectorsRecursive(child.File, connectors, rStudCandidates, inverted ^ child.invert, child.transform * transform);
                }
            }
        }
        bool DisableConnectorsRecursive(LDrawDatFile file)
        {
            if (studs.Contains(file.Name) ||
                studclipj.Contains(file.Name) ||
                rstud.Contains(file.Name) ||
                file.Name == "stud12" ||
                file.Name == "stud3" || file.Name == "stud3a" ||
                file.Name == "stud4h" || file.Name == "stud18a")
            {
                return true;
            }
            else
            {
                foreach (var child in file.Children)
                {
                    if (DisableConnectorsRecursive(child.File))
                        child.IsEnabled = false;
                }
                return false;
            }
        }
        public void DisableConnectors(LDrawDatFile file)
        {
            DisableConnectorsRecursive(file);
        }

        public void RemoveConnector(Connector c)
        {
            this.connectors.Remove(c);
            UpdateConnectorFile();
        }
        public void AddFromEdgeCrossing(LDrawDatFile file, Topology.Edge e0, Topology.Edge e1)
        {
            HashSet<Topology.Plane> planes0 = e0.Faces.Select(f => f.Plane).ToHashSet();
            HashSet<Topology.Plane> planes1 = e1.Faces.Select(f => f.Plane).ToHashSet();

            var plane = planes0.Intersect(planes1).FirstOrDefault();

            if (plane != null)
            {
                int vidx = -1;
                if (e0.v0.idx == e1.v0.idx ||
                    e0.v0.idx == e1.v1.idx)
                    vidx = 0;
                else if (e0.v1.idx == e1.v0.idx ||
                    e0.v1.idx == e1.v1.idx)
                    vidx = 1;
                Vector3[] pts = new Vector3[3];
                if (vidx == 0)
                {
                    pts[0] = e0.v1.pt;
                    pts[1] = e0.v0.pt;
                    pts[2] = e0.v0.idx == e1.v0.idx ? e1.v1.pt : e1.v0.pt;
                }
                else if (vidx == 1)
                {
                    pts[0] = e0.v0.pt;
                    pts[1] = e0.v1.pt;
                    pts[2] = e0.v1.idx == e1.v0.idx ? e1.v1.pt : e1.v0.pt;
                }

                List<Vector2> ppts = plane.ToPlanePts(pts);
                Vector2 dir0 = Vector2.Normalize(ppts[1] - ppts[0]);
                Vector2 ast = (ppts[0] + ppts[1]) * 0.5;
                Vector2 ad = new Vector2(-dir0.Y, dir0.X);
                Vector2 dir1 = Vector2.Normalize(ppts[2] - ppts[1]);
                Vector2 bs = (ppts[2] + ppts[1]) * 0.5;
                Vector2 bd = new Vector2(-dir1.Y, dir1.X);
                double dp = Vector2.Dot(dir0, dir1);

                double dx = bs.X - ast.X;
                double dy = bs.Y - ast.Y;
                double det = bd.X * ad.Y - bd.Y * ad.X;
                double u = (dy * bd.X - dx * bd.Y) / det;
                double v = (dy * ad.X - dx * ad.Y) / det;

                Vector2 p0 = ast + ad * u;
                Vector2 p1 = bs + bd * v;
                double len = (p0 - ppts[0]).Length();
                var meshpts = plane.ToMeshPts(new Vector2[] { p0 });


                Vector3 xdir, ydir;
                plane.GetRefDirs(out xdir, out ydir);
                Vector3 zdir = plane.normal;
                Matrix4x4 rot = new Matrix4x4(
                    xdir.X, xdir.Y, xdir.Z, 0,
                    ydir.X, ydir.Y, ydir.Z, 0,
                    zdir.X, zdir.Y, zdir.Z, 0,
                    0, 0, 0, 1);
                len *= 2;
                len = RoundTo(len, 0.01);
                Matrix4x4 cm = Matrix4x4.CreateScale(len) *
                    rot *
                    Matrix4x4.CreateTranslation(meshpts[0]);
                Connector c = new Connector() { Mat = cm, Type = ConnectorType.Clip, IsCustom = true };
                c.Changed += C_Changed;
                connectors.Add(c);
                UpdateConnectorFile();
            }
        }

        public void AddNew()
        {
            Connector c = new Connector() { Mat = connectors.Last().Mat, Type = ConnectorType.Clip, IsCustom = true };
            connectors.Add(c);
            UpdateConnectorFile();
        }
        double RoundTo(double v, double prec)
        {
            return Math.Truncate((v / prec) + 0.5) * prec;
        }

        private void C_Changed(object sender, bool e)
        {
            UpdateConnectorFile();
        }

        void UpdateConnectorFile()
        {
            List<Connector> customConnectors = this.connectors.Where(c => c.IsCustom).ToList();
            string jsonstr = JsonConvert.SerializeObject(customConnectors);
            File.WriteAllText(Path.Combine(LDrawFolders.ConnectorsFolder, partname + ".json"), jsonstr);
        }

        void LoadConnectorFile()
        {
            if (File.Exists(Path.Combine(LDrawFolders.ConnectorsFolder, partname + ".json")))
            {
                string jsonstr = File.ReadAllText(Path.Combine(LDrawFolders.ConnectorsFolder, partname + ".json"));

                List<Connector> customConnectors = JsonConvert.DeserializeObject<List<Connector>>(jsonstr);
                foreach (var c in customConnectors)
                {
                    c.Changed += C_Changed;
                    this.connectors.Add(c);
                }
            }
        }
    }
}