using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Xml.Linq;

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

        public abstract Visibility IconVisiblity { get; }
        public string Name { get => name; }
        public string FullPath { get => GetFullPath(); }
        protected string GetFullPath()
        {
            return parent != null ? Path.Combine(parent.GetFullPath(), name) :
                name;
        }

        public abstract System.Windows.Media.Brush ColorBrush { get; }
        public abstract IEnumerable<ScriptItem> Children { get; }
    }

    public class ScriptFile : ScriptItem
    {
        public ScriptFile(ScriptFolder p, string n) :
            base(p, n)
        { }

        public override IEnumerable<ScriptItem> Children => null;
        public override Visibility IconVisiblity => Visibility.Visible;


        public override System.Windows.Media.Brush ColorBrush
        {
            get
            {
                string ext = Path.GetExtension(this.Name).ToLower();
                if (ext == ".cs")
                    return System.Windows.Media.Brushes.LightGreen;
                else if (ext == ".gsl" || ext == ".glsl")
                    return System.Windows.Media.Brushes.MediumBlue;
                else
                    return System.Windows.Media.Brushes.Gray;
            }
        }
            
    }

    public class ScriptFolder : ScriptItem
    {
        List<ScriptItem> children = null;
        public ScriptFolder(ScriptFolder p, string n) :
            base(p, n)
        {

        }
        public override System.Windows.Media.Brush ColorBrush => System.Windows.Media.Brushes.Gray;
        public override Visibility IconVisiblity => Visibility.Collapsed;
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

        public void GetSources(List<Source> sources)
        {
            foreach (ScriptFolder folder in this.Children.Where(f => f is ScriptFolder))
            {
                folder.GetSources(sources);
            }

            sources.AddRange(
                this.Children.Where(f => f is ScriptFile && f.Name.EndsWith(".cs")).
                    Select(fname =>
                    new Source()
                    {
                        code = File.ReadAllText(fname.FullPath),
                        filepath = fname.FullPath
                    }));

        }
    }

}
