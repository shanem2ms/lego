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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            public List<string> aliases;
            bool includedInGame;
            public string mbxFilePath;
            public string mbxNum;
            public bool IncludeInGame
            {
                get => includedInGame; set
                { includedInGame = value; LDrawFolders.WriteIncludedGame(); }
            }

            public string GetDesc()
            {
                return $"mbx: {mbxNum}";
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

            string mbxPath => mbxFilePath != null ? Path.Combine(LDrawFolders.MdxFolder, mbxFilePath) : null;

            public string MbxPath => mbxPath;
            public bool HasMbx => mbxPath?.Length > 0;

        }
        public static string Root = @"C:\homep4\lego";
        static string rootFolder;
        public static string RootFolder => rootFolder ?? string.Empty;
        public static string MdxFolder => Path.Combine(RootFolder, "Mbx");
        static List<Entry> ldrawParts = new List<Entry>();
        static Dictionary<string, Entry> partPaths = new Dictionary<string, Entry>();
        static string selectedType;
        static Dictionary<string, List<string>> partsReverseLookup = new Dictionary<string, List<string>>();
        static Dictionary<string, string> aliases = new Dictionary<string, string>();
        static public bool FilterEnabled { get; set; } = false;
        public static string ConnectorsFolder;
        static public string SelectedType
        {
            get => selectedType;
            set { selectedType = value; }
        }

        public static string SearchString;
        public static IEnumerable<Entry> LDrawParts { get => SearchString?.Length > 0 ? GetSearchEntries() : GetCurrentGroupEntries(); }

        public static List<Entry> AllParts => ldrawParts;
        public static IEnumerable<string> LDrawGroups => 
            lDrawGroups.Where(kv => kv.Value.Any(v => v.includedInFilter)).Select(kv => kv.Key).OrderBy(s => s);
        static Dictionary<string, List<Entry>> lDrawGroups = new Dictionary<string, List<Entry>>();


        static IEnumerable<Entry> GetSearchEntries()
        {
            return ldrawParts.Where(e => e.name.StartsWith(SearchString));
        }
        static List<Entry> GetCurrentGroupEntries()
        {
            return selectedType == null ? new List<Entry>() :
                (FilterEnabled ? lDrawGroups[selectedType].Where(i => i.includedInFilter).ToList() :
                    lDrawGroups[selectedType]);
        }
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
                part.includedInFilter = part.HasMbx;
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

        public static Dictionary<string, List<string>> cacheTypeGroups;
        public static void SetCacheRoot(string folder)
        {
            Directory.CreateDirectory(folder);
            DirectoryInfo di = new DirectoryInfo(folder);

            if (!File.Exists(Path.Combine(folder, "categories.json")))
                return;
            HashSet<string> cacheItems = new HashSet<string>();
            StreamReader streamReader = new StreamReader(Path.Combine(folder, "categories.json"));
            string str = streamReader.ReadToEnd();
            JObject json = JObject.Parse(str);
            Dictionary<string, List<string>> typegroups = new Dictionary<string, List<string>>();
            foreach (var prop in json.Properties())
            {
                string grp = (string)prop.Value;
                List<string> items;
                if (!typegroups.TryGetValue(grp, out items))
                {
                    items = new List<string>();
                    typegroups.Add(grp, items);
                }
                items.Add(prop.Name);
            }

            var grps = typegroups.Where(t => t.Value.Count < 20);
            List<string> miscList = new List<string>();
            foreach (var grp in grps)
            {
                typegroups.Remove(grp.Key);
                miscList.AddRange(grp.Value);
            }
            typegroups.Add("Misc", miscList);
            cacheTypeGroups = typegroups;
        }
        public static void SetLDrawRoot(string folder)
        {
            string descFile = "partdesc.txt";
            rootFolder = folder;
            ConnectorsFolder = Path.Combine(rootFolder, @"partmake\connectors");
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
                        aliases.TryAdd(vals[0], vals[1]);
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
            /*

            Dictionary<string, List<string>> reverseAliases = new Dictionary<string, List<string>>();
            foreach (var kv in aliases)
            {
                List<string> aliases;
                if (!reverseAliases.TryGetValue(kv.Value, out aliases))
                {
                    aliases = new List<string>();
                    reverseAliases.Add(kv.Value, aliases);
                }
                aliases.Add(kv.Key);
            }*/

            Dictionary<string, Entry> partbyid = new Dictionary<string, Entry>();
            Regex rgnums = new Regex(@"\d+");
            foreach (var part in ldrawParts)
            {
                Match m = rgnums.Match(part.name);
                string name = m.Groups[0].Value;
                if (!partbyid.TryAdd(name, part))
                {
                    partbyid.TryAdd(name + "V2", part);
                }
                string pn = part.name.Substring(0, part.name.Length - 4);
                partbyid.TryAdd(pn, part);


            }

            {
                string[] partRelText = File.ReadAllLines(Path.Combine(Root, @"rebrickable\part_relationships.csv"));
                foreach (string prLine in partRelText)
                {
                    string[] vals = prLine.Split(',');
                    Match m0 = rgnums.Match(vals[1]);
                    Match m1 = rgnums.Match(vals[2]);
                    if (m1.Success && m0.Success && m1.Value != m0.Value)
                    {
                        Entry entry;
                        if (partbyid.TryGetValue(m0.Value, out entry))
                        {
                            partbyid.TryAdd(m1.Value, entry);
                        }
                    }
                }
            }

            Regex aliasRegex = new Regex(@"parts\\(\d+)");
            foreach (var kv in aliases)
            {
                Entry entry;
                Match m = aliasRegex.Match(kv.Key);
                if (!m.Success)
                    continue;
                Match m2 = rgnums.Match(kv.Value);
                string vl = m2.Groups[0].Value;
                if (partbyid.TryGetValue(kv.Value, out entry))
                {
                    string al = m.Groups[1].Value;
                    partbyid.TryAdd(al, entry);
                }
                else if (partbyid.TryGetValue(vl, out entry))
                {
                    string al = m.Groups[1].Value;
                    partbyid.TryAdd(al, entry);
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

            HashSet<string> mbxFiles = new HashSet<string>();
            DirectoryInfo mbxdir = new DirectoryInfo(LDrawFolders.MdxFolder);

            List<string> badlist = new List<string>();
            foreach (var fi in mbxdir.GetFiles("*.json"))
            {
                //string pname = fi.Name.Substring(0, fi.Name.Length-5).ToLower();
                Entry entry;
                Match m = rgnums.Match(fi.Name);
                string pname = m.Groups[0].Value;
                if (partbyid.TryGetValue(pname, out entry))
                {
                    entry.mbxFilePath = fi.Name;
                    entry.mbxNum = rgnums.Match(fi.Name).Value;
                }
                else
                    badlist.Add(pname);
            }

            var stayGrps = lDrawGroups.Where(kv => kv.Value.Count > 100);
            Dictionary<string, List<Entry>> newGrps = new Dictionary<string, List<Entry>>(
                lDrawGroups.Where(kv => kv.Value.Count > 10));
            var result = lDrawGroups.Where(kv => kv.Value.Count <= 10).SelectMany(kv => kv.Value).ToList();
            newGrps.Add("misc", result);
            lDrawGroups = newGrps;

            {
                string path = Path.Combine(LDrawFolders.Root, "ingame.txt");
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

            //string path = Path.Combine(LDrawFolders.Root, "ingame.txt");
            //File.WriteAllLines(path, fileLines);
        }
        public static void WriteAll(Func<int, int, string, bool> updateFunc)
        {            
            WriteAllCacheFiles(updateFunc);
        }

        public static void WriteSelected(List<Entry> parts)
        {
            string path = Path.Combine(LDrawFolders.Root, "cache");

            foreach (Entry part in parts)
            {
                LDrawDatFile df = GetPart(part);
                if (df.GetFaceCount() < 1000 || df.Name == "91405")
                {
                    Entry e = part;
                    string outname = e.mbxNum?.Length > 0 ?
                        e.mbxNum : df.Name;
                    df.WriteDescriptorFile(e, path, outname, true);
                    df.WriteCollisionFile(path, outname, true);
                    df.WriteMeshFile(path, outname);
                }
            }
        }
                
        static void WriteCategories(string path)
        {
            Dictionary<string, string> categories = new Dictionary<string, string>();
            DirectoryInfo di = new DirectoryInfo(path);
            foreach (FileInfo fi in di.GetFiles("*.json"))
            {
                StreamReader streamReader = new StreamReader(fi.FullName);
                string str = streamReader.ReadToEnd();
                JObject json = JObject.Parse(str);
                string jtype = (string)json["type"];
                string nm = Path.GetFileNameWithoutExtension(fi.Name);
                categories.Add(nm, jtype);
            }

            string outstr = JsonConvert.SerializeObject(categories);
            File.WriteAllText(Path.Combine(path, "categories.json"), outstr);
        }

        static void WriteAllCacheFiles(Func<int, int, string, bool> updateFunc)
        {
            string path = Path.Combine(LDrawFolders.Root, "cache");

            //if (Directory.Exists(path))
            //    Directory.Delete(path, true);
            Directory.CreateDirectory(path);
            List<Entry> partsToWrite = ldrawParts.Where(ld => ld.HasMbx).ToList();
            int completCnt = 0;
            int totalCnt = partsToWrite.Count;

            for (int i = 0; i < partsToWrite.Count; i++)
            {
                ThreadPool.QueueUserWorkItem((object o) =>
                {
                    int idx = (int)o;
                    LDrawDatFile df = GetPart(partsToWrite[idx]);
                    if (df.GetFaceCount() < 1000 || df.Name == "91405")
                    {
                        Entry e = partsToWrite[idx];
                        string outname = e.mbxNum?.Length > 0 ?
                            e.mbxNum : df.Name;
                        df.WriteDescriptorFile(e, path, outname, false);
                        df.WriteCollisionFile(path, outname, false);
                        df.WriteMeshFile(path, outname);
                    }
                    int completed = Interlocked.Increment(ref completCnt);
                    if (completed >= totalCnt)
                        WriteCategories(path);
                    updateFunc(completed, totalCnt, partsToWrite[idx].name);
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
            string path = Path.Combine(LDrawFolders.Root, "cache", _name);
            LDrWrite(_name, transform, path, true);
            path += ".hr_mesh";
            ldrLoader.LoadCached(path, out vertices, out indices);
        }

        public static void LDrWrite(string _name, Matrix4x4 transform, string outPath, bool force)
        {
            Entry e = GetEntry(_name + ".dat");
            LdrLoader ldrLoader = new LdrLoader();
            ldrLoader.Write(rootFolder, e.name,
                transform, outPath, force);
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
