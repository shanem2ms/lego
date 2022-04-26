using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Numerics;

namespace partmake
{

    public static class LDrawFolders
    {
        public class Entry : IComparable<Entry>
        {
            public string type;
            public string subtype;
            public string name;
            public string path;
            public LDrawDatFile file;
            public string desc;
            public float[] dims;
            public bool ismainpart;
            public string[] subparts;
            public bool includedInFilter;
            bool includedInGame;
            public bool IncludeInGame
            {
                get => includedInGame; set
                { includedInGame = value; LDrawFolders.WriteIncludedGame(); }
            }

            float totalDim { get { float ret = 1; if (dims != null) { foreach (var d in dims) ret *= d; } return ret; } }

            public int CompareTo(Entry other)
            {
                int cmp = totalDim.CompareTo(other.totalDim);
                if (cmp != 0) return cmp;
                return ToString().CompareTo(other.ToString());
            }

            public override string ToString()
            {
                string[] dimarr = dims?.Select(d => d.ToString()).ToArray();
                string dimstr = dimarr != null ? string.Join("x", dimarr) : "";
                return string.Format("{0} {1} {2}", type, dimstr, desc);
            }

            string MdxPath => Path.Combine(LDrawFolders.MdxFolder, Path.ChangeExtension(this.name, "json"));
            public bool HasMdx => File.Exists(MdxPath);

        }
        static string rootFolder;
        public static string RootFolder => rootFolder ?? string.Empty;
        public static string MdxFolder => Path.Combine(RootFolder, "Mbx");
        static List<Entry> ldrawParts = new List<Entry>();
        static Dictionary<string, Entry> partPaths = new Dictionary<string, Entry>();
        static string selectedType;
        static Dictionary<string, List<string>> partsReverseLookup = new Dictionary<string, List<string>>();
        static Dictionary<string, string> aliases = new Dictionary<string, string>();
        static public bool FilterEnabled { get; set; } = false;
        static public string SelectedType
        {
            get => selectedType;
            set { selectedType = value; }
        }
        public static List<Entry> LDrawParts { get => selectedType == null ? new List<Entry>() :
                (FilterEnabled ? lDrawGroups[selectedType].Where(i => i.includedInFilter).ToList() :
                    lDrawGroups[selectedType]); }

        public static List<Entry> AllParts => ldrawParts;
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

        static public void ApplyFilterMdx()
        {
            foreach (var part in AllParts)
            {
                part.includedInFilter = part.HasMdx;
            }
        }

        static public List<string> ReverseLookup(string part)
        {
            List<string> result;
            string p = part + ".dat";
            Entry e = GetEntry(p);
            if (!partsReverseLookup.TryGetValue(p, out result))
                    result = null;
            return result;
        }

        static public List<string> IncludedInParts(string subpart)
        {
            HashSet<string> mainParts = new HashSet<string>();


            string p = subpart + ".dat";
            HashSet<string> subParts = new HashSet<string>() { p };
            while (subParts.Count > 0)
            {
                HashSet<string> nextSubParts = new HashSet<string>();
                foreach (var part in subParts)
                {
                    List<string> results;
                    if (partsReverseLookup.TryGetValue(part, out results))
                    {
                        foreach (var r in results) nextSubParts.Add(r);
                    }
                    else
                        mainParts.Add(part);
                }
                subParts = nextSubParts;
            }
            var outParts = mainParts.ToList();
            outParts.Sort();
            return outParts;
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
                    string subtype;
                    HashSet<string> subParts = new HashSet<string>();
                    using (StreamReader sr = new StreamReader(fullName))
                    {
                        bool skip = false;
                        string aliasname = null;
                        string descline = null;
                        while (!sr.EndOfStream)
                        {
                            line = sr.ReadLine();
                            if (line == null)
                            {
                                skip = true;
                                break;
                            }
                            if (descline == null && line.Length > 0)
                                descline = line;
                            string lt = line.Trim();
                            if (lt.Length == 0)
                                continue;

                            if (lt[0] == '1')
                            {
                                Regex r1 = new Regex(@"1\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\.\d]+)\s+([-\w\\]+\.dat)$");
                                Match m = r1.Match(lt);
                                if (m.Groups.Count >= 15)
                                {
                                    subParts.Add(m.Groups[14].Value);
                                }
                            }

                            string mvto = @"0 ~Moved to ";
                            string alof = @"0 // Alias of";
                            if (line.StartsWith(mvto))
                            {
                                aliasname = line.Substring(mvto.Length);
                                break;
                            }
                            else if (line.StartsWith(alof))
                            {
                                aliasname = line.Substring(alof.Length);
                                break;
                            }
                        }
                        if (skip)
                            continue;
                        if (aliasname != null)
                        {
                            aliases.Add(Path.Combine(reldir, name), aliasname);
                            aliasname = null;
                            continue;
                        }
                        Match tm = r.Match(descline);
                        if (tm.Groups.Count < 2)
                            continue;
                        typestr = tm.Groups[1].Value;
                        typestr = typestr.TrimStart('_');
                        descline = descline.Substring(tm.Index + tm.Length);

                        Match psm = rdim.Match(descline);
                        subtype = descline.Substring(0, psm.Index).Trim();
                        subtype = subtype.Replace('/', '\\');
                        string[] dims = null;
                        if (psm.Groups.Count > 1)
                        {
                            List<string> dimstr = new List<string>();
                            foreach (var c in psm.Groups[1].Captures)
                                dimstr.Add(c.ToString());
                            foreach (var c in psm.Groups[3].Captures)
                                dimstr.Add(c.ToString());
                            dims = dimstr.ToArray();

                            dim = string.Join(" ", dims);
                            descline = descline.Substring(psm.Index + psm.Length);
                        }

                        desc = descline.Trim();
                    }
                    sw.WriteLine($"{name}//{typestr}//{subtype}//{dim}//{desc}//{reldir}");
                    if (subParts.Count > 0)
                    {
                        sw.WriteLine("   " + string.Join("//", subParts));
                    }
                    else
                        sw.WriteLine("");
                }
                foreach (var al in aliases)
                {
                    sw.WriteLine($"{al.Key}//{al.Value}//a");
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
                    if (vals.Length == 3)
                    {
                        aliases.Add(vals[0], vals[1]);
                        continue;
                    }
                    string line2 = sr.ReadLine();
                    if (vals[3].Length > 0)
                    {
                        string[] dstr = vals[3].Split(' ');
                        dims = new float[dstr.Length];
                        for (int i = 0; i < dims.Length; ++i)
                        {
                            dims[i] = float.Parse(dstr[i]);
                        }
                    }
                    line2 = line2.Trim();
                    List<string> subParts;
                    if (line2.Length > 0)
                    {
                        subParts = new List<string>();

                    }
                    string desc = vals[4].Trim();
                    string type = vals[1];
                    string subtype = vals[2];
                    if (subtype.Length > 0)
                        type += " " + subtype;
                    else if (desc.Length > 0)
                        type += " Mod";
                    Entry e = new Entry()
                    {
                        path = Path.Combine(rootFolder, vals[5], name),
                        name = name,
                        type = type,
                        subtype = subtype,
                        desc = desc,
                        dims = dims,
                        ismainpart = (vals[5] == "parts"),
                        subparts = line2.Length > 0 ? line2.Split("//") : null
                    };

                    partPaths.Add(Path.Combine(vals[5], name).ToLower(), e);
                }
            }

            foreach (var pp in partPaths)
            {
                if (pp.Value.subparts != null)
                {
                    foreach (var sp in pp.Value.subparts)
                    {
                        List<string> spList;
                        if (!partsReverseLookup.TryGetValue(sp, out spList))
                        {
                            spList = new List<string>();
                            partsReverseLookup.Add(sp, spList);
                        }
                        spList.Add(pp.Value.name);
                    }
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
                lDrawGroups.Where(kv => kv.Value.Count > 10));
            var result = lDrawGroups.Where(kv => kv.Value.Count <= 10).SelectMany(kv => kv.Value).ToList();
            newGrps.Add("misc", result);
            lDrawGroups = newGrps;

            {
                string path = @"C:\homep4\lego\ingame.txt";
                string[] lines = File.ReadAllLines(path);
                foreach (string line in lines)
                {
                    ldrawParts.First(e => e.name == line).IncludeInGame = true;
                }
            }
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

        static public void WriteIncludedGame()
        {

            List<string> fileLines = new List<string>();
            foreach (Entry e in ldrawParts)
            {
                if (e.IncludeInGame)
                {
                    fileLines.Add(e.name);
                }
            }

            string path = @"C:\homep4\lego\ingame.txt";
            File.WriteAllLines(path, fileLines);
        }
        public static void WriteAll()
        {            
            WriteAllCacheFiles();
        }
        static void WriteAllCacheFiles()
        {
            string path = @"C:\homep4\lego\cache";
            //if (Directory.Exists(path))
            //    Directory.Delete(path, true);
            Directory.CreateDirectory(path);
            for (int i = 0; i < ldrawParts.Count; i++)
            {
                ThreadPool.QueueUserWorkItem((object o) =>
                {
                    int idx = (int)o;
                    if (!ldrawParts[idx].HasMdx)
                        return;
                    LDrawDatFile df = GetPart(ldrawParts[idx]);
                    if (df.GetFaceCount() > 1000 && df.Name != "91405")
                        return;
                    df.WriteConnectorFile(path);
                    df.WriteCollisionFile(path);
                    df.WriteMeshFile(path);
                }, i);
            }

        }

        public static string Alias(string name)
        {
            string aliasname;
            if (aliases.TryGetValue(Path.Combine("parts", name), out aliasname))
                return aliasname + ".dat";
            if (aliases.TryGetValue(Path.Combine("p", name), out aliasname))
                return aliasname + ".dat";
            return null;
        }

        public static Entry GetEntry(string _name)
        {
            string name = _name.ToLower();

            string al = Alias(name);
            if (al != null)
                name = al;
            Entry e;
            if (!partPaths.TryGetValue(Path.Combine("parts", name), out e) &&
                !partPaths.TryGetValue(Path.Combine("p", name), out e))
                return null;
            return e;
        }

        public static void GetLDrLoader(string _name, Matrix4x4 transform, out LdrLoader.PosTexcoordNrmVertex []
            vertices, out int []indices)
        {
            Entry e = GetEntry(_name + ".dat");
            LdrLoader ldrLoader = new LdrLoader();
            //ldrLoader.Load(rootFolder, e.name,
            //    transform,
            //    out vertices, out indices);
            string path = @"C:\homep4\lego\cache\" + _name;
            LDrWrite(_name, transform, path);
            path += ".hr_mesh";
            ldrLoader.LoadCached(path, out vertices, out indices);
        }

        public static void LDrWrite(string _name, Matrix4x4 transform, string outPath)
        {
            Entry e = GetEntry(_name + ".dat");
            LdrLoader ldrLoader = new LdrLoader();
            ldrLoader.Write(rootFolder, e.name,
                transform, outPath);
        }
        public static LDrawDatFile GetPart(string _name)
        {
            string name = _name.ToLower();
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
