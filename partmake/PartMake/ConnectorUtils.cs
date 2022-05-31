using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.DoubleNumerics;
using KdTree;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Collections;

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
        MFigArmKnob = 11
    }
    public class Connector : IComparable<Connector>
    {
        public ConnectorType Type { get; set; }
        public Matrix4x4 Mat { get; set; }

        public Vector3 Offset => Mat.Translation;

        public Quaternion Rotation => Quaternion.CreateFromRotationMatrix(Mat);

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
    }

    public class Connectors
    {

        List<Tuple<Vector3, Vector3>> bisectors = null;
        List<Connector> connectors = new List<Connector>();

        public List<Connector> Items => connectors;

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

        public Connectors(LDrawDatFile f)
        {
        }
        public void Init(LDrawDatFile thisfile)
        {
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
                    Mat = Matrix4x4.CreateScale(4, 4, 4) * m *
                    Matrix4x4.CreateTranslation(rstud.Item1),
                    Type = ConnectorType.RStud
                });
            }
            //Topology.ConnectorUtils.FindLoops(GetTopoMesh());
            this.connectors = connectors.Distinct().ToList();
        }

        Connector CreateBaseConnector(LDrawDatFile file, Matrix4x4 mat, ConnectorType type)
        {
            AABB bb = file.GetBBox();
            Vector3 scl = bb.Max - bb.Min;
            Vector3 off = (bb.Max + bb.Min) * 0.5f;
            Matrix4x4 cm = Matrix4x4.CreateScale(scl) *
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
                file.Name == "4-4cylo")
                && inverted)
            {
                Vector3 scale = transform.GetScale();
                if (Eps.Eq2(scale.X, 3) &&
                    Eps.Eq2(scale.Z, 3))
                {
                    connectors.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(0, -0.5, 0) * transform,
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
            }
            else if (file.Name == "knob1")
            {
                connectors.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(0, 0, 0) * transform,
                    ConnectorType.MFigArmKnob));
            }
            else if (file.Name == "hipstud")
            {
                Vector3 scale = transform.GetScale();
                {
                    connectors.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(0, 5, 0) * transform,
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

        public void AddFromEdgeCrossing(LDrawDatFile file, Topology.Edge e0, Topology.Edge e1)
        {
            HashSet<Topology.Plane> planes0 = e0.Faces.Select(f => f.Plane).ToHashSet();
            HashSet<Topology.Plane> planes1 = e1.Faces.Select(f => f.Plane).ToHashSet();

            var plane = planes0.Intersect(planes1).FirstOrDefault();

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
            var meshpts = plane.ToMeshPts(new Vector2[] { p0 });

            connectors.Add(CreateBaseConnector(file, Matrix4x4.CreateTranslation(meshpts[0]),
                ConnectorType.Clip));

        }
    }

    namespace Topology
    {
        public class ConnectorUtils
        {
            static public Dictionary<int, Plane> GetCandidatePlanes(Topology.Mesh m)
            {
                Dictionary<int, Plane> planes = new Dictionary<int, Plane>();
                Plane bottomPlane = m.planeMgr.GetPlane(Vector3.UnitY, 0);
                foreach (Face f in m.faces)
                {
                    if (f.Plane.totalArea > 25 || f.Plane.idx == bottomPlane.idx)
                    {
                        if (!planes.ContainsKey(f.Plane.idx))
                            planes.Add(f.Plane.idx, f.Plane);
                    }
                }

                Dictionary<int, Plane> planes2 = new Dictionary<int, Plane>();
                foreach (var kv in planes)
                {
                    Plane p = kv.Value;
                    bool[] pn = { false, false };
                    foreach (var v in m.vertices)
                    {
                        double dist = Vector3.Dot(v.pt, p.normal) - p.d;
                        if (!Eps.Eq2(dist, 0))
                        {
                            if (dist < 0) pn[0] = true;
                            else pn[1] = true;
                        }
                    }
                    if (pn[0] ^ pn[1])
                        planes2.Add(kv.Key, kv.Value);
                }
                return planes2;
            }
            static public List<Tuple<Vector3, Topology.Plane>> GetRStuds(Topology.Mesh m, Vector3[] candidates, List<Tuple<Vector3, Vector3>> bisectors)
            {
                Dictionary<int, Plane> planes = GetCandidatePlanes(m);
                AABB aabb = AABB.CreateFromPoints(m.vertices.Select(v => v.pt).ToList());
                List<Tuple<Vector3, Plane>> outPts = new List<Tuple<Vector3, Plane>>();
                foreach (var kvplane in planes)
                {
                    List<Face> planeFaces =
                        m.faces.Where(f => f.Plane.idx == kvplane.Key).ToList();
                    foreach (var kv in m.edgeDict)
                    {
                        kv.Value.flag = 0;
                    }
                    HashSet<Edge> edges = new HashSet<Edge>();
                    List<Vector3> candidatePts = new List<Vector3>();
                    {
                        KdTree<double, Vector3> kd = new KdTree<double, Vector3>(3, new KdTree.Math.DoubleMath());
                        m.GetPlaneEdges(kvplane.Value, edges);

                        if (edges.Count == 0)
                            continue;

                        Vector3 planeNormal = kvplane.Value.normal;
                        double planeDist = Vector3.Dot(edges.First().v0.pt, planeNormal);
                        foreach (Vector3 c in candidates)
                        {
                            Vector3 pt = c - planeNormal * Vector3.Dot(c, planeNormal);
                            pt += planeNormal * planeDist;
                            if (Mesh.IsUnique(kd, pt))
                                candidatePts.Add(pt);
                        }

                        foreach (var edge in edges)
                        {
                            edge.flag = 1;
                        }
                        foreach (var edge in edges)
                        {
                            if (edge.len < 6.0f)
                                continue;
                            foreach (Edge e in edge.v0.edges)
                            {
                                if (e == edge || e.flag != 1 || e.len < 6.0f)
                                    continue;
                                Vector3 v0 = edge.v0.pt;
                                Vector3 v1a = edge.v1.pt;
                                Vector3 v1adir = Vector3.Normalize(v1a - v0);
                                Vector3 v1b = (edge.v0.idx == e.v0.idx) ? e.v1.pt : e.v0.pt;
                                Vector3 v1bdir = Vector3.Normalize(v1b - v0);

                                Vector3 bisectAngle = Vector3.Normalize(v1adir + v1bdir);
                                double sinTheta = Vector3.Cross(bisectAngle, v1adir).Length();
                                Vector3 testPt = v0 + (6.0f / sinTheta) * bisectAngle;
                                bisectors.Add(new Tuple<Vector3, Vector3>(v0, testPt));
                                if (Mesh.IsUnique(kd, testPt))
                                    candidatePts.Add(testPt);
                            }

                            foreach (Edge e in edge.v1.edges)
                            {
                                if (e == edge || e.flag != 1 || e.len < 6.0f)
                                    continue;

                                Vector3 v0 = edge.v1.pt;
                                Vector3 v1a = edge.v0.pt;
                                Vector3 v1adir = Vector3.Normalize(v1a - v0);
                                Vector3 v1b = (edge.v1.idx == e.v0.idx) ? e.v1.pt : e.v0.pt;
                                Vector3 v1bdir = Vector3.Normalize(v1b - v0);

                                Vector3 bisectAngle = Vector3.Normalize(v1adir + v1bdir);
                                double sinTheta = Vector3.Cross(bisectAngle, v1adir).Length();
                                Vector3 testPt = v0 + (6.0f / sinTheta) * bisectAngle;
                                bisectors.Add(new Tuple<Vector3, Vector3>(v0, testPt));
                                if (Mesh.IsUnique(kd, testPt))
                                    candidatePts.Add(testPt);
                            }

                            if (Eps.Eq(edge.len, 12))
                            {
                                Vector3 edgeNrm = Vector3.Cross(planeNormal, edge.dir);
                                Vector3 pt1 = (edge.v0.pt + edge.v1.pt) * 0.5f +
                                    edgeNrm * 6.0f;
                                if (Mesh.IsUnique(kd, pt1))
                                    candidatePts.Add(pt1);
                                Vector3 pt2 = (edge.v0.pt + edge.v1.pt) * 0.5f -
                                    edgeNrm * 6.0f;
                                if (Mesh.IsUnique(kd, pt2))
                                    candidatePts.Add(pt2);
                            }
                        }
                    }

                    foreach (Vector3 cpt in candidatePts)
                    {
                        List<Tuple<Edge, Vector3>> touchedEdges = new List<Tuple<Edge, Vector3>>();
                        bool hasBlockage = false;
                        foreach (var e in edges)
                        {
                            Vector3 nearestPt = Mesh.NearestPt(e.v0.pt, e.v1.pt, cpt);

                            Vector3 nrmVec = (nearestPt - cpt);
                            double lenSq = (nrmVec).LengthSquared();
                            if (lenSq > 34 && lenSq < 38)
                            {
                                touchedEdges.Add(new Tuple<Edge, Vector3>(e, Vector3.Normalize(nrmVec)));
                            }
                            else if (lenSq < 34)
                                hasBlockage = true;
                        }

                        if (hasBlockage)
                            continue;
                        if (touchedEdges.Count() >= 3)
                        {
                            bool isOnFace =
                                planeFaces.Any(f => f.aabb.Contains(cpt) != AABB.ContainmentType.Disjoint
                                && f.IsPointOnPoly(cpt));
                            if (!isOnFace)
                            {
                                double minDot = 1;
                                for (int i = 0; i < touchedEdges.Count; i++)
                                {
                                    for (int j = i + 1; j < touchedEdges.Count; j++)
                                    {
                                        double dot = Vector3.Dot(touchedEdges[i].Item2, touchedEdges[j].Item2);
                                        minDot = Math.Min(minDot, dot);
                                    }
                                }
                                if (minDot < 0)
                                    outPts.Add(new Tuple<Vector3, Plane>(cpt, kvplane.Value));
                            }
                        }
                    }
                }
                outPts = outPts.Distinct().ToList();
                return outPts;
            }

            static public List<Tuple<Vector3, Topology.Plane>> GetCandidates(Topology.Mesh m, Plane p,
                List<Tuple<Vector3, Vector3>> bisectors, float minsize,
                float minradius, float maxradius)
            {
                AABB aabb = AABB.CreateFromPoints(m.vertices.Select(v => v.pt).ToList());
                List<Tuple<Vector3, Plane>> outPts = new List<Tuple<Vector3, Plane>>();

                List<Face> planeFaces =
                    m.faces.Where(f => f.Plane.idx == p.idx).ToList();
                foreach (var kv in m.edgeDict)
                {
                    kv.Value.flag = 0;
                }
                HashSet<Edge> edges = new HashSet<Edge>();
                List<Vector3> candidatePts = new List<Vector3>();
                {
                    KdTree<double, Vector3> kd = new KdTree<double, Vector3>(3, new KdTree.Math.DoubleMath());
                    m.GetPlaneEdges(p, edges);

                    if (edges.Count == 0)
                        return null;

                    Vector3 planeNormal = p.normal;

                    foreach (var edge in edges)
                    {
                        edge.flag = 1;
                    }
                    foreach (var edge in edges)
                    {
                        if (edge.len < minsize)
                            continue;
                        foreach (Edge e in edge.v0.edges)
                        {
                            if (e == edge || e.flag != 1 || e.len < minsize)
                                continue;
                            if (!edges.Contains(e))
                                continue;
                            Vector3 v0 = edge.v0.pt;
                            Vector3 v1a = edge.v1.pt;
                            Vector3 v1adir = Vector3.Normalize(v1a - v0);
                            Vector3 v1b = (edge.v0.idx == e.v0.idx) ? e.v1.pt : e.v0.pt;
                            Vector3 v1bdir = Vector3.Normalize(v1b - v0);

                            Vector3 bisectAngle = Vector3.Normalize(v1adir + v1bdir);
                            double sinTheta = Vector3.Cross(bisectAngle, v1adir).Length();
                            Vector3 testPt = v0 + (minsize / sinTheta) * bisectAngle;
                            bisectors.Add(new Tuple<Vector3, Vector3>(v0, testPt));
                            if (Mesh.IsUnique(kd, testPt))
                                candidatePts.Add(testPt);
                        }

                        foreach (Edge e in edge.v1.edges)
                        {
                            if (e == edge || e.flag != 1 || e.len < minsize)
                                continue;
                            if (!edges.Contains(e))
                                continue;

                            Vector3 v0 = edge.v1.pt;
                            Vector3 v1a = edge.v0.pt;
                            Vector3 v1adir = Vector3.Normalize(v1a - v0);
                            Vector3 v1b = (edge.v1.idx == e.v0.idx) ? e.v1.pt : e.v0.pt;
                            Vector3 v1bdir = Vector3.Normalize(v1b - v0);

                            Vector3 bisectAngle = Vector3.Normalize(v1adir + v1bdir);
                            double sinTheta = Vector3.Cross(bisectAngle, v1adir).Length();
                            Vector3 testPt = v0 + (minsize / sinTheta) * bisectAngle;
                            bisectors.Add(new Tuple<Vector3, Vector3>(v0, testPt));
                            if (Mesh.IsUnique(kd, testPt))
                                candidatePts.Add(testPt);
                        }
                    }
                }

                foreach (Vector3 cpt in candidatePts)
                {
                    List<Tuple<Edge, Vector3>> touchedEdges = new List<Tuple<Edge, Vector3>>();
                    bool hasBlockage = false;
                    foreach (var e in edges)
                    {
                        Vector3 nearestPt = Mesh.NearestPt(e.v0.pt, e.v1.pt, cpt);

                        Vector3 nrmVec = (nearestPt - cpt);
                        double lenSq = (nrmVec).LengthSquared();
                        if (lenSq > minradius && lenSq < maxradius)
                        {
                            touchedEdges.Add(new Tuple<Edge, Vector3>(e, Vector3.Normalize(nrmVec)));
                        }
                        else if (lenSq < minradius)
                            hasBlockage = true;
                    }

                    if (hasBlockage)
                        continue;
                    if (touchedEdges.Count() >= 3)
                    {
                        bool isOnFace =
                            planeFaces.Any(f => f.aabb.Contains(cpt) != AABB.ContainmentType.Disjoint
                            && f.IsPointOnPoly(cpt));
                        if (!isOnFace)
                        {
                            double minDot = 1;
                            for (int i = 0; i < touchedEdges.Count; i++)
                            {
                                for (int j = i + 1; j < touchedEdges.Count; j++)
                                {
                                    double dot = Vector3.Dot(touchedEdges[i].Item2, touchedEdges[j].Item2);
                                    minDot = Math.Min(minDot, dot);
                                }
                            }
                            if (minDot < 0)
                                outPts.Add(new Tuple<Vector3, Plane>(cpt, p));
                        }
                    }
                }
                outPts = outPts.Distinct().ToList();
                return outPts;
            }


            static public void FindCirlces(Mesh m, Plane p)
            {
                KdTree<double, Vector3> kd = new KdTree<double, Vector3>(3, new KdTree.Math.DoubleMath());
                HashSet<Edge> planeEdges = new HashSet<Edge>();
                m.GetPlaneEdges(p, planeEdges);
                foreach (Edge e in planeEdges)
                {
                    e.flag = 0;
                }
                foreach (Edge e in planeEdges)
                {
                    FollowEdge(e, planeEdges);
                }

            }

            static void FollowEdge(Edge e, HashSet<Edge> planeEdges)
            {
                e.flag = 1;
                foreach (Edge e1 in e.v1.edges)
                {
                    if (e.flag == 1)
                        continue;
                    Vector3.Dot(e.dir, e1.dir);
                }
                e.flag = 0;
            }
            static public List<Loop> FindLoops(Mesh m)
            {
                List<Loop> foundLoops = new List<Loop>();
                foreach (Face f in m.faces)
                {
                    Vector3 nrm = f.Normal;
                    foreach (EdgePtr eptr in f.edges)
                    {
                        Edge e = eptr.e;
                        if (e.flag == 0)
                        {
                            List<Edge> edges = new List<Edge>() { e };
                            if (FollowCoplanarEdges(edges, nrm))
                            {
                                foreach (Edge fe in edges)
                                {
                                    fe.flag = 1;
                                }
                                foundLoops.Add(new Loop() { edges = edges });
                            }
                        }
                    }
                }

                return foundLoops;
            }

            static bool FollowCoplanarEdges(List<Edge> edges, Vector3 nrm)
            {
                Edge cur = edges.Last();
                if (cur.instack)
                {
                    if (edges[0] == cur && edges.Count > 3)
                        return true;
                    else
                        return false;
                }
                cur.instack = true;
                foreach (Edge e in cur.v1.edges)
                {
                    if (e.flag == 0 &&
                        Vector3.Dot(nrm, e.dir) == 0)
                    {
                        edges.Add(e);
                        if (FollowCoplanarEdges(edges, nrm))
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

        }
    }
}