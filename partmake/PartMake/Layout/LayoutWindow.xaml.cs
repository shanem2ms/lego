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

namespace partmake
{
    /// <summary>
    /// Interaction logic for LayoutWindow.xaml
    /// </summary>
    public partial class LayoutWindow : UserControl, INotifyPropertyChanged
    {
        public IEnumerable<string> CacheGroups { get => LDrawFolders.LDrawGroups; }
        ScriptEngine scriptEngine;
        public string FilterText { get; set; }

        public IEnumerable<Palette.Item> Colors { get => Palette.SortedItems; }
        public List<string> ScriptFiles { get; set; }

        string scriptFolder;

        public List<Part> FilteredItems { get; set; }

        public ObservableCollection<ScriptTextEditor> OpenEditors { get; } = new ObservableCollection<ScriptTextEditor>();

        Scene scene = new Scene();
        public string SelectedType
        {
            get => LDrawFolders.SelectedType;
            set
            {
                LDrawFolders.SelectedType = value;
            }
        }

        void WriteLine(string line)
        {
            OutputTB.AppendText(line + "\n");
            OutputTB.ScrollToEnd();
        }
        ScriptTextEditor ActiveScriptTB;
        public LayoutWindow()
        {
            this.DataContext = this;
            InitializeComponent();

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CacheGroups"));

            MCStructure mcs = new MCStructure(@"C:\Users\shane\Documents\manvillage.mcstructure");
            scriptEngine = new ScriptEngine();
            script.Api.WriteLine = WriteLine;

            scriptFolder = System.IO.Path.Combine(LDrawFolders.Root, "Partmake\\Scripts");
            FilteredItems = LDrawFolders.CacheItems.ToList();
            _LayoutControl.Vis.scene = scene;
            _LayoutControl.Vis.OnPartPicked += Vis_OnPartPicked;
            _LayoutControl.Vis.OnConnectorPicked += Vis_OnConnectorPicked;
            _LayoutControl.Vis.DrawDebug = DrawBulletDebug;
            scene.DebugDrawLine =
                _LayoutControl.Vis.BulletDebugDrawLine;
            RefrehScriptsFolder();
            foreach (var file in ScriptFiles)
            {
                OpenFile(file);
            }
            RunScript();
        }

        void DrawBulletDebug()
        {
            scene.DrawBulletDebug();
        }

        private void Vis_OnConnectorPicked(object sender, LayoutVis.PartPickEvent e)
        {
            WriteLine($"Connector {e.connectorIdx}");
            WriteLine(e.part.item.Connectors[e.connectorIdx].ToString());
        }

        private void Vis_OnPartPicked(object sender, LayoutVis.PartPickEvent e)
        {
        }

        void OpenFile(string name)
        {
            string filepath = 
                System.IO.Path.Combine(scriptFolder, name);
            ScriptTextEditor ScriptTB = new ScriptTextEditor(filepath);
            ScriptTB.Engine = scriptEngine;
            OpenEditors.Add(ScriptTB);
        }
        void RefrehScriptsFolder()
        {
            DirectoryInfo di = new DirectoryInfo(scriptFolder);
            this.ScriptFiles = 
                di.GetFiles("*.cs").Select(fi => fi.Name).ToList();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ScriptFiles"));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void LDrawFolders_Initialized(object sender, bool e)
        {
        }

        private void _RenderControl_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            //partVis.OnKeyDown(e);
        }

        private void _RenderControl_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            //partVis.OnKeyUp(e);
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
                editor.Save();
            }
            RunScript();
        }

        void RunScript()
        {
            try
            {
                List<string> allFiles = this.ScriptFiles.Select(fname => File.ReadAllText(Path.Combine(scriptFolder, fname))).ToList();
                scriptEngine.Run(allFiles, scene, _LayoutControl.Vis);
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
                ActiveScriptTB.SaveAs(sfd.FileName);
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

}
