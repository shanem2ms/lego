using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace partmake
{
    public abstract class ScriptItem
    {
        public ScriptItem(ScriptFolder p, string n)
        {
            parent = p;
            name = n;
        }

        ScriptFolder parent;
        string name;

        public string Name { get => name; }
        public string FullPath { get => GetFullPath(); }
        protected string GetFullPath()
        {
            return parent != null ? Path.Combine(parent.GetFullPath(), name) :
                name;
        }
        public abstract IEnumerable<ScriptItem> Children { get; }
    }

    public class ScriptFile : ScriptItem
    {
        public ScriptFile(ScriptFolder p, string n) :
            base(p, n)
        { }

        public override IEnumerable<ScriptItem> Children => null;
    }

    public class ScriptFolder : ScriptItem
    {
        List<ScriptItem> children = null;
        public ScriptFolder(ScriptFolder p, string n) :
            base(p, n)
        {

        }

        public override IEnumerable<ScriptItem> Children
        {
            get
            {
                if (this.children == null)
                {
                    DirectoryInfo di = new DirectoryInfo(GetFullPath());
                    this.children = new List<ScriptItem>();
                    children.AddRange(
                        di.GetDirectories("*.*").Select(di => new ScriptFolder(this, di.Name)));
                    children.AddRange(
                        di.GetFiles("*.*").Select(fi => new ScriptFile(this, fi.Name)));
                }
                return this.children;
            }
        }

        public List<Source> GetSources()
        {
            return this.Children.Where(f => f.Name.EndsWith(".cs")).
                    Select(fname =>
                    new Source()
                    {
                        code = File.ReadAllText(fname.FullPath),
                        filepath = fname.Name
                    }).ToList();
        }
    }

}
