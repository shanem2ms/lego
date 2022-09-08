using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ConstrainedExecution;

namespace partmake
{
    /// <summary>
    /// Interaction logic for LayoutWindow.xaml
    /// </summary>
    public partial class LayoutWindow : UserControl, INotifyPropertyChanged
    {
        public IEnumerable<string> CacheGroups { get => LDrawFolders.LDrawGroups; }
        ScriptEngine scriptEngine;

        public ScriptEngine Engine => scriptEngine;
        public string FilterText { get; set; }

        public IEnumerable<Palette.Item> Colors { get => Palette.SortedItems; }

        ScriptFolder scriptFolder;

        public IEnumerable<ScriptItem> ScriptFiles
        {
            get => scriptFolder?.Children;
        }

        public List<Part> FilteredItems { get; set; }

        public ObservableCollection<Editor> OpenEditors { get; } = new ObservableCollection<Editor>();

        Scene scene = new Scene();
        public string SelectedType
        {
            get => LDrawFolders.SelectedType;
            set
            {
                LDrawFolders.SelectedType = value;
            }
        }

        bool bulletDebugDrawEnabled = false;
        void Clear()
        {
            OutputTB.Text = "";
        }
        void WriteLine(string line)
        {
            OutputTB.AppendText(line + "\n");
            OutputTB.ScrollToEnd();
        }
        //ScriptTextEditor ActiveScriptTB;
        public LayoutWindow()
        {
            this.DataContext = this;
            InitializeComponent();

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CacheGroups"));

            MCStructure mcs = new MCStructure(@"C:\Users\shane\Documents\manvillage.mcstructure");
            scriptEngine = new ScriptEngine();
            script.Api.WriteLine = WriteLine;

            scriptFolder = new ScriptFolder(null,
                System.IO.Path.Combine(LDrawFolders.Root, "Partmake\\Scripts"));
            script.Api.ScriptFolder = scriptFolder.Name;
            FilteredItems = LDrawFolders.CacheItems.ToList();
            _LayoutControl.Vis.scene = scene;
            _LayoutControl.Vis.OnPartPicked += Vis_OnPartPicked;
            _LayoutControl.Vis.OnConnectorPicked += Vis_OnConnectorPicked;
            _LayoutControl.Vis.DrawDebug = DrawBulletDebug;
            _LayoutControl.Vis.AfterDeviceCreated += Vis_AfterDeviceCreated;
            scene.DebugDrawLine =
                _LayoutControl.Vis.BulletDebugDrawLine;
            RefrehScriptsFolder();
        }

        private void Vis_AfterDeviceCreated(object sender, bool e)
        {
            RunScript();
        }

        void DrawBulletDebug()
        {
            if (bulletDebugDrawEnabled)
                scene.DrawBulletDebug();
        }

        private void Vis_OnConnectorPicked(object sender, LayoutVis.PartPickEvent e)
        {
            WriteLine($"Connector {e.connectorIdx}");
            WriteLine(e.part.item.Connectors[e.connectorIdx].ToString());
        }

        private void Vis_OnPartPicked(object sender, LayoutVis.PartPickEvent e)
        {
            WriteLine($"Part {e.part.item.Name}");
            WriteLine($"Pos {Vector3.Transform(Vector3.Zero, e.part.mat)}");
            foreach (var tile in e.part.octTiles)
            {
                WriteLine($"Tile {tile.x} {tile.y} {tile.z}");
            }
        }

        void OpenFile(string filepath)
        {
            if (!OpenEditors.Any(f => f.FilePath == filepath))
            {
                OpenEditors.Add(new Editor()
                {
                    FilePath = filepath,
                    Control = new ScriptTextEditor(filepath, this.Engine)
                });
            }
        }
        void RefrehScriptsFolder()
        {
            //DirectoryInfo di = new DirectoryInfo(scriptFolder);
            //this.ScriptFiles = 
            //    di.GetFiles("*.*").Select(fi => fi.Name).ToList();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ScriptFiles"));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void LDrawFolders_Initialized(object sender, bool e)
        {
        }

        private void _RenderControl_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            scene.OnKeyDown(e);
        }

        private void _RenderControl_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            scene.OnKeyUp(e);
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {

        }

        //@"((\d+)x(\d+)(x(\d+))?)|(\w+)"
        Regex dimsRegex = new Regex(@"((\d+)x(\d+)(x(\d+))?)|(\w+)");
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = (sender as TextBox).Text;
            text = text.Trim();
            if (text.Length == 0)
            {
                FilteredItems = LDrawFolders.CacheItems.ToList();
            }
            else
            {
                List<Part> curItems = LDrawFolders.CacheItems.ToList();
                MatchCollection mc = dimsRegex.Matches(text);
                foreach (Match m in mc)
                {
                    if (m.Groups[2].Length > 0)
                    {
                        List<Part> nextItems = new List<Part>();
                        float[] d = new float[] {
                            float.Parse(m.Groups[2].Value),
                            float.Parse(m.Groups[3].Value),
                            m.Groups[4].Value.Length > 0 ? float.Parse(m.Groups[4].Value) : 1 };
                        foreach (var item in curItems)
                        {
                            bool match = true;
                            if (item.DimsF == null)
                                continue;
                            for (int i = 0; i < 3; i++)
                            {
                                if (d[i] != item.DimsF[i])
                                    match = false;
                            }
                            if (match)
                                nextItems.Add(item);
                        }
                        curItems = nextItems;
                    }
                    else if (m.Groups[6].Length > 0)
                    {
                        curItems = curItems.Where(ci => 
                        ci.MainType.ToLower().Contains(m.Groups[6].Value.ToLower()) ||
                        ci.Desc.ToLower().Contains(m.Groups[6].Value.ToLower())).ToList();
                    }
                }
                FilteredItems = curItems;
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FilteredItems"));
        }

        private void ItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (Part cai in e.AddedItems)
            {
            }
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {          
            foreach (var editor in OpenEditors)
            {
                editor.Control.Save();
            }
            RunScript();
        }
        private void SriptFile_Click(object sender, RoutedEventArgs e)
        {
            ScriptFile file = (sender as Button).DataContext as ScriptFile;
            if (file != null)
                OpenFile(file.FullPath);
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            Editor editor = (sender as Button).DataContext as Editor;
            this.OpenEditors.Remove(editor);
        }
        private void BulletDebug_Click(object sender, RoutedEventArgs e)
        {
            bulletDebugDrawEnabled = !bulletDebugDrawEnabled;
        }
        
        void RunScript()
        {
            try
            {
                Clear();
                List<Source> sources = new List<Source>();
                this.scriptFolder.GetSources(sources);
                scriptEngine.Run(sources, scene, _LayoutControl.Vis);
            }
            catch(Exception e)
            {

            }
        }
        private void SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog sfd = new Microsoft.Win32.SaveFileDialog();
            sfd.InitialDirectory = LDrawFolders.Root;
            sfd.DefaultExt = ".cs"; // Default file extension
            sfd.Filter = "CSharp Scripts (.cs)|*.cs"; // Filter files by extension
            if (sfd.ShowDialog() == true)
            {
                //ActiveScriptTB.SaveAs(sfd.FileName);
            }
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            //scriptEngine.Run(ActiveScriptTB.Text, _LayoutControl.Vis);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            
        }
    }

    public class Editor : IEquatable<Editor>
    {
        public string FilePath { get; set; }
        public ScriptTextEditor Control { get; set; }
        public bool Equals(Editor other)
        {
            return FilePath.Equals(other.FilePath);
        }

        public int GetHashCode([DisallowNull] Editor obj)
        {
            return obj.FilePath.GetHashCode();
        }
    }
    public static class ChildWindHelper
    {
        public static void GetChildrenOfType<T>(this DependencyObject depObj, List<T> children)
            where T : DependencyObject
        {
            if (depObj == null) return;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);

                if (child is T)
                    children.Add(child as T);
                else
                    GetChildrenOfType<T>(child, children);
            }
        }

        public static T FindParent<T>(this DependencyObject child) where T : DependencyObject
        {
            //get parent item
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            //we've reached the end of the tree
            if (parentObject == null) return null;

            //check if the parent matches the type we're looking for
            T parent = parentObject as T;
            if (parent != null)
                return parent;
            else
                return FindParent<T>(parentObject);
        }
    }

}
