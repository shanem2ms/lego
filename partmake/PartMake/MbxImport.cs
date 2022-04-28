using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;
using System.Numerics;

namespace partmake
{
    public class MbxImport
    {
        public class Mesh
        {
            public List<Vector3> vertices { get; set; }
            public List<int> indices { get; set; }

            public void Append(Mesh other)
            {
                int cnt = vertices.Count;
                indices.AddRange(
                    other.indices.Select(i => i + cnt));
                vertices.AddRange(other.vertices);
            }

            public Mesh Transform(Matrix4x4 mat)
            {
                return new Mesh()
                {
                    vertices = vertices.Select(v => Vector3.Transform(v, mat)).ToList(),
                    indices = indices
                };
            }
        }
        public Dictionary<string, Mesh> partGeo = new Dictionary<string, Mesh>();

        bool is_bit_set(int num, int bit)
        {
            return (num & (1 << bit)) != 0;
        }

        class DetailConfig
        {
            public Vector3 pos;
            public Quaternion rot;
            public int type;
        }
        public MbxImport(string filename)
        {
            using (var file = File.OpenRead(filename))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
            {
                List<Mesh> knobMeshes = new List<Mesh>();
                Dictionary<string, List<DetailConfig>> knobConfigs 
                    = new Dictionary<string, List<DetailConfig>>();
                foreach (var entry in zip.Entries)
                {
                    using (var stream = entry.Open())
                    {
                        StreamReader streamReader = new StreamReader(stream);
                        string str = streamReader.ReadToEnd();
                        JObject json = JObject.Parse(str);
                        JObject details = json["details"] as JObject;
                        JToken knobs = details["knobs"];
                        foreach (JToken knob in knobs.Children())
                        {
                            if (knob.Type == JTokenType.Property)
                            {
                                JProperty p = (JProperty)knob;
                                Mesh mesh = ReadGeometry(p.Value as JObject);
                                knobMeshes.Add(mesh);
                            }
                        }

                        JToken configs = json["configurations"];
                        foreach (JToken c in configs.Children())
                        {
                            if (c.Type == JTokenType.Property)
                            {
                                JProperty geom = (JProperty)c;
                                foreach (JProperty partp in geom.Value.Children())
                                {
                                    JObject partV = partp.Value as JObject;
                                    int version = (int)partV["version"];
                                    string geoname = (string)partV["geometry"]["file"];

                                    if (knobConfigs.ContainsKey(geoname))
                                        continue;
                                    JArray knobsA = partV["geometry"]["extras"]["knobs"] as JArray;
                                    int kcnt = knobsA.Count;
                                    List<DetailConfig> kconfigs = new List<DetailConfig>();
                                    knobConfigs.Add(geoname, kconfigs);
                                    for (int i = 0; i < kcnt; i++)
                                    {
                                        JArray jpos = knobsA[i]["transform"]["position"] as JArray;
                                        JArray jquat = knobsA[i]["transform"]["quaternion"] as JArray;
                                        int ktype = (int)knobsA[i]["type"];
                                        kconfigs.Add(new DetailConfig()
                                        {
                                            pos = new Vector3((float)jpos[0], (float)jpos[1], (float)jpos[2]),
                                            rot = new Quaternion((float)jquat[0], (float)jquat[1], (float)jquat[2], (float)jquat[3]),
                                            type = ktype
                                        });
                                    }
                                }
                            }
                        }

                        JToken geometries = json["geometries"];
                        foreach (JToken geo in geometries.Children())
                        {
                            if (geo.Type == JTokenType.Property)
                            {
                                JProperty geom = (JProperty)geo;
                                foreach (JProperty partp in geom.Value.Children())
                                {
                                    JObject partV = partp.Value as JObject;
                                    Mesh mesh = ReadGeometry(partV);
                                    if (knobConfigs.ContainsKey(partp.Name))
                                    {
                                        List<DetailConfig> cfgList = knobConfigs[partp.Name];
                                        foreach (DetailConfig cfg in cfgList)
                                        {
                                            Mesh knobmesh = knobMeshes[cfg.type - 1].Transform(
                                                Matrix4x4.CreateFromQuaternion(cfg.rot) *
                                                Matrix4x4.CreateTranslation(cfg.pos));
                                            mesh.Append(knobmesh);
                                        }
                                    }
                                    partGeo.Add(partp.Name, mesh);
                                }
                            }
                        }
                    }
                }
            }

        }

        Mesh ReadGeometry(JObject partV)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> indices = new List<int>();
            JArray varray = partV["vertices"] as JArray;
            for (int idx = 0; idx < varray.Count; idx += 3)
            {
                vertices.Add(new Vector3((float)varray[idx],
                    (float)varray[idx + 1],
                    (float)varray[idx + 2]));
            }
            JArray uvs = partV["uvs"] as JArray;
            int uvLayers = uvs != null ? uvs.Count : 0;
            JArray farray = partV["faces"] as JArray;
            int offset = 0;
            while (offset < farray.Count)
            {
                int type = (int)farray[offset++];
                bool isQuad = is_bit_set(type, 0);
                bool hasMaterial = is_bit_set(type, 1);
                bool hasFaceUv = is_bit_set(type, 2);
                bool hasFaceVertexUv = is_bit_set(type, 3);
                bool hasFaceNormal = is_bit_set(type, 4);
                bool hasFaceVertexNormal = is_bit_set(type, 5);
                bool hasFaceColor = is_bit_set(type, 6);
                bool hasFaceVertexColor = is_bit_set(type, 7);

                if (isQuad)
                {
                    int a = (int)farray[offset++];
                    int b = (int)farray[offset++];
                    int c = (int)farray[offset++];
                    int d = (int)farray[offset++];
                    indices.Add(a);
                    indices.Add(b);
                    indices.Add(c);
                    indices.Add(a);
                    indices.Add(c);
                    indices.Add(d);
                }
                else
                {
                    int a = (int)farray[offset++];
                    int b = (int)farray[offset++];
                    int c = (int)farray[offset++];
                    indices.Add(a);
                    indices.Add(b);
                    indices.Add(c);
                }
                if (hasMaterial)
                    offset++;

                for (int uvidx = 0; uvidx < uvLayers; uvidx++)
                {
                    if (hasFaceUv)
                        offset++;
                    if (hasFaceVertexUv)
                        offset += isQuad ? 4 : 3;
                }

                if (hasFaceNormal)
                    offset++;

                if (hasFaceVertexNormal)
                    offset += isQuad ? 4 : 3;
                if (hasFaceColor)
                    offset++;

                if (hasFaceVertexColor)
                    offset += isQuad ? 4 : 3;
            }

            return new Mesh() { vertices = vertices, indices = indices };
        }
        public void CompareAll()
        {
            foreach (var kv in this.partGeo)
            {
                string name = Path.ChangeExtension(kv.Key, "dat");
                AABB aABB = AABB.CreateFromPoints(kv.Value.vertices.Select(v => new System.DoubleNumerics.Vector3(v.X, v.Y, v.Z) * 2.5));
                LDrawDatFile part = LDrawFolders.GetPart(name);
                if (part == null)
                {
                    Debug.WriteLine($"Skipping {name}");
                    continue;
                }
                List<Vtx> allVertices = new List<Vtx>();
                part.GetVertices(allVertices, false);
                AABB aABB2 = AABB.CreateFromPoints(allVertices.Select(v => new System.DoubleNumerics.Vector3(v.pos.X, v.pos.Y, v.pos.Z)));
                System.DoubleNumerics.Vector3 ext = aABB.Max - aABB.Min;
                System.DoubleNumerics.Vector3 ext2 = aABB2.Max - aABB2.Min;

                Debug.WriteLine($" {allVertices.Count} {kv.Value.vertices.Count} [{ext.X / ext2.X}, {ext.Y / ext2.Y}, {ext.Z / ext2.Z}]");
            }
        }
        public void WriteAll()
        {
            Directory.CreateDirectory(LDrawFolders.MdxFolder);
            foreach (var kv in this.partGeo)
            {
                string jsonstr = JsonConvert.SerializeObject(kv.Value);
                string outfile = Path.GetFileNameWithoutExtension(kv.Key) + ".json";
                File.WriteAllText(Path.Combine(LDrawFolders.MdxFolder, outfile), jsonstr);
            }
        }

        public static Mesh LoadMeshfile(string path)
        {
            if (!File.Exists(path))
                return null;
            string text = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<Mesh>(text);
        }
    }
}
