using Amazon.S3.Model.Internal.MarshallTransformations;
using Newtonsoft.Json;
using partmake.Properties;
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

    public class ScriptSettings
    {
        public struct Item
        {
            public bool enabled;
            public string name;
        }

        public List<Item> items = null;
    }
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

        bool enabled = true;
        public bool Enabled { 
            get => enabled; set {
                enabled = value;
                parent.NeedsConfigUpdate();
            } }
        protected string GetFullPath()
        {
            return parent != null ? Path.Combine(parent.GetFullPath(), name) :
                name;
        }

        public abstract void NeedsConfigUpdate();
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

        public override void NeedsConfigUpdate()
        {
            throw new NotImplementedException();
        }
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
        ScriptSettings settings = null;
        
        public ScriptFolder(ScriptFolder p, string n) :
            base(p, n)
        {
            if (File.Exists(Path.Combine(FullPath, "config.json")))
                LoadSettings();
            else
                WriteSettings();
        }

        public override void NeedsConfigUpdate()
        {
            WriteSettings();
        }

        void LoadSettings()
        {
            string settingsstr = File.ReadAllText(Path.Combine(FullPath, "config.json"));
            this.settings = JsonConvert.DeserializeObject<ScriptSettings>(settingsstr);
            var itemsDict = this.settings.items.ToDictionary(t => t.name);
            foreach (var child in Children)
            {
                ScriptSettings.Item item;
                if (itemsDict.TryGetValue(child.Name, out item))
                {
                    child.Enabled = item.enabled;
                }
            }
        }

        void WriteSettings()
        {
            settings = new ScriptSettings();
            settings.items = new List<ScriptSettings.Item>(
                Children.Select(c => new ScriptSettings.Item() { enabled = c.Enabled, name = c.Name }));
            string settingsstr = JsonConvert.SerializeObject(settings);
            File.WriteAllText(Path.Combine(FullPath, "config.json"), settingsstr);
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
                        di.GetFiles("*.cs").Select(fi => new ScriptFile(this, fi.Name)));
                    children.AddRange(
                        di.GetFiles("*.glsl").Select(fi => new ScriptFile(this, fi.Name)));
                }
                return this.children;
            }
        }

        public void GetSources(List<Source> sources)
        {
            foreach (ScriptFolder folder in this.Children.Where(f => f is ScriptFolder && f.Enabled))
            {
                folder.GetSources(sources);
            }

            sources.AddRange(
                this.Children.Where(f => f is ScriptFile && f.Enabled && f.Name.EndsWith(".cs")).
                    Select(fname =>
                    new Source()
                    {
                        code = File.ReadAllText(fname.FullPath),
                        filepath = fname.FullPath
                    }));

        }
    }

}
