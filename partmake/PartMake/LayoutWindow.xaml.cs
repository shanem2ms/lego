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
using System.Windows.Shapes;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Globalization;
using System.IO;

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

        public List<CacheItem> FilteredItems { get; set; }
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
        public LayoutWindow()
        {
            this.DataContext = this;
            InitializeComponent();

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CacheGroups"));

            MCStructure mcs = new MCStructure(@"C:\Users\shane\Documents\manvillage.mcstructure");
            scriptEngine = new ScriptEngine();
            scriptEngine.WriteLine = WriteLine;

            FilteredItems = LDrawFolders.CacheItems.ToList();
            ScriptTB.Engine = scriptEngine;
            ScriptTB.Load(@"C:\homep4\lego\partmake\Script.cs");
            _LayoutControl.Vis.OnPartPicked += Vis_OnPartPicked;
        }

        private void Vis_OnPartPicked(object sender, int e)
        {
            WriteLine($"Part picked {e}");
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
                List<CacheItem> curItems = LDrawFolders.CacheItems.ToList();
                MatchCollection mc = dimsRegex.Matches(text);
                foreach (Match m in mc)
                {
                    if (m.Groups[2].Length > 0)
                    {
                        List<CacheItem> nextItems = new List<CacheItem>();
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
            foreach (CacheItem cai in e.AddedItems)
            {
            }
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            ScriptTB.Save(@"C:\homep4\lego\partmake\Script.cs");
            scriptEngine.Run(ScriptTB.Text, _LayoutControl.Vis);
        }
        private void SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog sfd = new Microsoft.Win32.SaveFileDialog();
            sfd.InitialDirectory = LDrawFolders.Root;
            sfd.DefaultExt = ".cs"; // Default file extension
            sfd.Filter = "CSharp Scripts (.cs)|*.cs"; // Filter files by extension
            if (sfd.ShowDialog() == true)
            {
                ScriptTB.Save(sfd.FileName);
            }
        }
        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            ScriptTB.Save(@"C:\homep4\lego\partmake\Script.cs");
            scriptEngine.Run(ScriptTB.Text, _LayoutControl.Vis);
        }
    }

}
