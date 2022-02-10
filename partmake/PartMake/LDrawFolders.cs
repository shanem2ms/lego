using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace partmake
{

    public static class LDrawFolders
    {
        public class Entry : IComparable<Entry>
        {
            public string type;
            public string name;
            public string path;
            public LDrawDatFile file;
            public string desc;
            public float[] dims;
            public bool ismainpart;

            public int CompareTo(Entry other)
            {
                return ToString().CompareTo(other.ToString());
            }

            public override string ToString()
            {
                string[] dimarr = dims?.Select(d => d.ToString()).ToArray();
                string dimstr = dimarr != null ? string.Join("x", dimarr) : "";
                return string.Format("{0} {1} {2}", type, dimstr, desc);
            }
        }
        static string rootFolder;
        static List<Entry> ldrawParts = new List<Entry>();
        static Dictionary<string, Entry> partPaths = new Dictionary<string, Entry>();
        static string selectedType;
        static public string SelectedType
        {
            get => selectedType;
            set { selectedType = value; }
        }
        public static List<Entry> LDrawParts { get => selectedType == null ? new List<Entry>() : 
                lDrawGroups[selectedType]; }
        public static IEnumerable<string> LDrawGroups => lDrawGroups.Keys;
        static Dictionary<string, List<Entry>> lDrawGroups = new Dictionary<string, List<Entry>>();
        static void GetFilesRecursive(DirectoryInfo di, List<string> filepaths)
        {
            foreach (DirectoryInfo dichild in di.GetDirectories())
            {
                GetFilesRecursive(dichild, filepaths);
            }

            foreach (FileInfo fi in di.GetFiles("*.dat"))
            {
                filepaths.Add(fi.FullName);
            }
        }
        public static void SetRoot(string folder)
        {
            string descFile = "partdesc.txt";
            rootFolder = folder;
            if (!File.Exists(Path.Combine(folder, descFile)))
            {
                List<string> allFiles = new List<string>();
                DirectoryInfo di = new DirectoryInfo(folder);
                GetFilesRecursive(di, allFiles);

                StreamWriter sw = new StreamWriter(Path.Combine(folder, descFile + ".tmp"));
                Regex r = new Regex(@"0\s[~=]?(\w+)");
                Regex rdim = new Regex(@"([\d\.]+)(\s+x\s+([\d\.]+))+");
                foreach (string fullName in allFiles)
                {
                    string relname = Path.GetRelativePath(folder, fullName);
                    string name = Path.GetFileName(relname);
                    string reldir = Path.GetDirectoryName(relname);
                    string line = "";
                    string dim = "";
                    string typestr;
                    string desc;
                    using (StreamReader sr = new StreamReader(fullName))
                    {
                        bool skip = false;
                        string descline = null;
                        while (line.Length == 0 || line[0] == '0')
                        {
                            line = sr.ReadLine();
                            if (line == null)
                            {
                                skip = true;
                                break;
                            }
                            if (descline == null && line.Length > 0)
                                descline = line;
                            /*
                            if (line.StartsWith(@"0 ~Moved to") ||
                                line.StartsWith(@"0 // Alias of"))
                            {
                                skip = true;
                                break;
                            } */
                        }
                        if (skip)
                            continue;
                        Match tm = r.Match(descline);
                        if (tm.Groups.Count < 2)
                            continue;
                        typestr = tm.Groups[1].Value;
                        typestr = typestr.TrimStart('_');
                        descline = descline.Substring(tm.Index + tm.Length);

                        Match psm = rdim.Match(descline);
                        string[] dims = null;
                        if (psm.Groups.Count > 1)
                        {
                            dims = new string[psm.Groups.Count / 2];
                            for (int i = 0; i < psm.Groups.Count; i += 2)
                            {
                                dims[i / 2] = psm.Groups[i + 1].Value;
                            }

                            dim = string.Join(" ", dims);
                            descline = descline.Substring(psm.Index + psm.Length);
                        }

                        desc = descline.Trim();
                    }
                    sw.WriteLine("{0}//{1}//{2}//{3}//{4}", name, typestr, dim, desc, reldir);
                }
                sw.Close();
                File.Move(Path.Combine(folder, descFile + ".tmp"), Path.Combine(folder, descFile));
            }


            using (StreamReader sr = new StreamReader(Path.Combine(folder, descFile)))
            {
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    string[] vals = line.Split("//");
                    string name = vals[0];
                    float[] dims = null;
                    if (vals[2].Length > 0)
                    {
                        string[] dstr = vals[2].Split(' ');
                        dims = new float[dstr.Length];
                        for (int i = 0; i < dims.Length; ++i)
                        {
                            dims[i] = float.Parse(dstr[i]);
                        }
                    }
                    Entry e = new Entry()
                    {
                        path = Path.Combine(rootFolder, vals[4], name),
                        name = name,
                        type = vals[1],
                        desc = vals[3],
                        dims = dims,
                        ismainpart = (vals[4] == "parts")
                    };

                    partPaths.Add(Path.Combine(vals[4], name).ToLower(), e);
                }
            }

            
            ldrawParts.Sort();
            string nomats = "nomaterials.txt";
            if (!File.Exists(Path.Combine(folder, nomats)))
            {
                StreamWriter sw = new StreamWriter(Path.Combine(folder, nomats + ".tmp"));
                foreach (var kv in partPaths)
                {
                    if (!kv.Value.ismainpart)
                        continue;
                    LDrawDatFile df = GetPart(kv.Value);
                    if (!df.IsMultiColor)
                        sw.WriteLine(kv.Key);
                }
                sw.Close();
                File.Move(Path.Combine(folder, nomats + ".tmp"), Path.Combine(folder, nomats));
            }

            using (StreamReader sr = new StreamReader(Path.Combine(folder, nomats)))
            {
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    ldrawParts.Add(partPaths[line]);
                }
            }

            ldrawParts.Sort();

            foreach (var part in ldrawParts)
            {
                List<Entry> dictGroups;
                if (!lDrawGroups.TryGetValue(part.type, out dictGroups))
                {
                    dictGroups = new List<Entry>();
                    lDrawGroups.Add(part.type, dictGroups);
                }
                dictGroups.Add(part);
            }

            var stayGrps = lDrawGroups.Where(kv => kv.Value.Count > 100);
            Dictionary<string, List<Entry>> newGrps = new Dictionary<string, List<Entry>>(
                lDrawGroups.Where(kv => kv.Value.Count > 100));
            var result = lDrawGroups.Where(kv => kv.Value.Count <= 100).SelectMany(kv => kv.Value).ToList();
            newGrps.Add("misc", result);
            lDrawGroups = newGrps;
        }

        public static void LoadAll()
        {
            for (int i = 0; i < ldrawParts.Count; i++)
            {
                if (i % 100 == 0)
                    Debug.WriteLine(i);
                LDrawDatFile df = GetPart(ldrawParts[i]);
            }
        }

        public static void WriteAll()
        {
            string path = @"C:\homep4\lego\connectors";
            for (int i = 0; i < ldrawParts.Count; i++)
            {
                if (i % 100 == 0)
                    Debug.WriteLine(i);
                LDrawDatFile df = GetPart(ldrawParts[i]);
                df.WriteConnectorFile(path);
            }
            
        }

        public static Entry GetEntry(string name)
        {
            Entry e;
            if (!partPaths.TryGetValue(Path.Combine("parts", name), out e) &&
                !partPaths.TryGetValue(Path.Combine("p", name), out e))
                return null;
            return e;
        }
        public static LDrawDatFile GetPart(string name)
        {
            Entry e = GetEntry(name);
            if (e == null)
                return null;
            if (e.file == null)
                e.file = new LDrawDatFile(e.path);
            return e.file;
        }

        public static LDrawDatFile GetPart(Entry e)
        {
            if (e.file == null)
                e.file = new LDrawDatFile(e.path);
            return e.file.Clone();
        }

        public static string GetDesc(string name)
        {
            if (name == null)
                return null;
            Entry e = GetEntry(name);
            return e.desc;
        }
    }
}
