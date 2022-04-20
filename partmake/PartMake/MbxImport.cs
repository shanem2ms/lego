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
        }
        public Dictionary<string, Mesh> partGeo = new Dictionary<string, Mesh>();

        public MbxImport(string filename)
        {
            using (var file = File.OpenRead(filename))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    using (var stream = entry.Open())
                    {
                        StreamReader streamReader = new StreamReader(stream);
                        string str = streamReader.ReadToEnd();
                        JObject json = JObject.Parse(str);
                        JToken geometries = json["geometries"];
                        foreach (JToken geo in geometries.Children())
                        {                           
                            if (geo.Type == JTokenType.Property)
                            {
                                JProperty geom = (JProperty)geo;
                                foreach (JProperty partp in geom.Value.Children())
                                {
                                    JObject partV = partp.Value as JObject;
                                    JArray varray = partV["vertices"] as JArray;
                                    List<Vector3> vertices = new List<Vector3>();
                                    for (int idx = 0; idx < varray.Count; idx +=3)
                                    {
                                        vertices.Add(new Vector3((float)varray[idx],
                                            (float)varray[idx+1],
                                            (float)varray[idx+2]));
                                    }
                                    JArray farray = partV["faces"] as JArray;
                                    List<int> indices = new List<int>();
                                    for (int idx = 0; idx < farray.Count; idx ++)
                                    {
                                        indices.Add((int)farray[idx]);
                                    }
                                    partGeo.Add(partp.Name, new Mesh() { vertices = vertices, indices = indices });
                                }
                            }
                        }
                    }
                }
            }

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
