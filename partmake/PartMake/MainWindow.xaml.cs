using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Linq;
using System.Timers;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Data;
using System.Globalization;
using System.DoubleNumerics;
using Microsoft.Win32;

namespace partmake
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public IEnumerable<LDrawFolders.Entry> LDrawParts
        {
            get
            {
                return LDrawFolders.LDrawParts;
            }
        }
        public IEnumerable<string> LDrawGroups { get => LDrawFolders.LDrawGroups; }

        LDrawFolders.Entry selectedItem = null;
        LDrawDatFile selectedPart = null;
        string textFilter = "";
        PartVis vis = null;

        public string SelectedItemDesc { get { return selectedItem?.GetDesc(); } }        
        public string SelectedItemMatrix { get {
                if (selectedPart == null)
                    return "";
                Vector3 scale;
                Quaternion rotation;
                Vector3 translate;
                Matrix4x4.Decompose(selectedPart.PartMatrix, out scale, out rotation, out translate);
                return $"s {scale}\nr {rotation}\nt {translate}"; } }
        public LDrawDatFile CurrentDatFile => selectedPart;
        public List<LDrawDatNode> CurrentPart { get => new List<LDrawDatNode>() { new LDrawDatNode { File = selectedPart } }; }

        public event PropertyChangedEventHandler PropertyChanged;

        LDrawDatNode selectedNode = null;
        public LDrawDatNode SelectedNode => selectedNode;
        
        List<Topology.INode> SelectedINodes = new List<Topology.INode>();
        public Topology.INode SelectedINode
        {
            get => SelectedINodes.Count > 0 ? SelectedINodes[0] : null; set
            { SelectedINodes.Clear(); SelectedINodes.Add(value); }
        }
        public string Log => selectedPart?.GetTopoMesh().LogString;

        LDrawDatFile.Connector selectedConnector = null;
        public LDrawDatFile.Connector SelectedConnector
        {
            get
            { return selectedConnector; }
            set
            { selectedConnector = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedConnector")); }
        }

        public Topology.BSPNode SelectedBSPNode => vis?.SelectedBSPNode;
        public List<Topology.BSPNode> BSPNodes =>
            new List<Topology.BSPNode>() { selectedPart?.GetTopoMesh().bSPTree?.Top };

        public bool disableConnectors = false;
        public bool DisableConnectors {get => disableConnectors; set { disableConnectors = value; Rebuild(); } }

        public Topology.Settings TopoSettings { get; } = new Topology.Settings();
        public string SelectedType
        {
            get => LDrawFolders.SelectedType;
            set
            {
                LDrawFolders.SelectedType = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("LDrawParts"));
            }
        }

        public LDrawFolders.Entry SelectedItem
        {
            get { return selectedItem; }
            set
            {
                selectedItem = value;
                OnSelectedItem(selectedItem);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItem"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItemDesc"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItemMatrix"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItemConnectors"));
            }
        }

        public MainWindow()
        {
            System.IO.Directory.CreateDirectory(
                LDrawFolders.Root + "\\cache");
            Topology.Mesh.settings = this.TopoSettings;
            Topology.Mesh.settings.SettingsChanged += Settings_SettingsChanged;
            LDrawFolders.SetRoot(@"C:\homep4\lego\ldraw");
            this.DataContext = this;
            InitializeComponent();
            string part = "4733.dat";
            if (File.Exists("PartMake.ini"))
            {
                string[] lines = File.ReadAllLines("PartMake.ini");
                if (lines.Length > 0)
                    part = lines[0];
            }
            SelectedItem = LDrawFolders.GetEntry(part);
            vis = _RenderControl.Vis;
            vis.Part = selectedPart;
            EpsTB.Text = Eps.Epsilon.ToString();
            vis.OnINodeSelected += Vis_OnINodeSelected;
            vis.OnBSPNodeSelected += Vis_OnBSPNodeSelected;
            vis.OnLogUpdated += Vis_OnLogUpdated;
            LDrawFolders.ApplyFilterMdx();
            LDrawFolders.FilterEnabled = true;
            FilteredCheckbox.IsChecked = true;
        }

        private void Vis_OnLogUpdated(object sender, string e)
        {
            polyLog.LogText = e;
        }

        void Rebuild()
        {
            selectedPart.ClearTopoMesh();
            if (disableConnectors)
                selectedPart.DisableConnectors();
            vis.Part = selectedPart;
        }
        private void Settings_SettingsChanged(object sender, EventArgs e)
        {
            Rebuild();
        }

        private void Vis_OnBSPNodeSelected(object sender, Topology.BSPNode e)
        {
            SelectBSPNode(e);
        }

        private void Vis_OnINodeSelected(object sender, Topology.INode e)
        {
            bool addToSelection = (System.Windows.Input.Keyboard.GetKeyStates(System.Windows.Input.Key.LeftShift) == System.Windows.Input.KeyStates.Down ||
                System.Windows.Input.Keyboard.GetKeyStates(System.Windows.Input.Key.RightShift) == System.Windows.Input.KeyStates.Down);
            SelectINode(e, addToSelection);
        }

        void SelectINode(Topology.INode e, bool addToSelection)
        {
            if (!addToSelection)
            {
                foreach (var n in this.SelectedINodes)
                    n.IsSelected = false;
                this.SelectedINodes.Clear();
            }
            if (e != null)
            {
                e.IsSelected = true;
                this.SelectedINodes.Insert(0, e);
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedINode"));

        }

        void OnSelectedItem(LDrawFolders.Entry item)
        { 
            if (item == null)
                return;
            selectedPart = LDrawFolders.GetPart(item);
            selectedPart.SetSubPartSizes();
            if (disableConnectors)
                selectedPart.DisableConnectors();
            if (vis != null)
                vis.Part = selectedPart;
            
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentDatFile"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentPart"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Log"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("BSPNodes"));
            File.WriteAllLines("PartMake.ini", new string[] { item.name });
        }

        private void TextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            textFilter = (sender as TextBox).Text;
            textFilter = textFilter.Trim();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("LDrawParts"));
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue != null)
                (e.NewValue as LDrawDatNode).IsSelected = true;
            if (e.OldValue != null)
                (e.OldValue as LDrawDatNode).IsSelected = false;
            selectedNode = (e.NewValue as LDrawDatNode);
            vis.SelectedNode = selectedNode;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedNode"));
            //threeD.Refresh();
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            Rebuild();
        }

        private void WriteAll_Button_Click(object sender, RoutedEventArgs e)
        {
            LDrawFolders.WriteAll((completed, total, name) => {
                Dispatcher.BeginInvoke(new Action(() =>
                    StatusTb.Text = $"[{completed} / {total}]  [{name}]"));
                return true;
            });
        }
        private void ImportMbx_Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Multiselect = true;
            dlg.FileName = "ZMBX Files"; // Default file name
            dlg.DefaultExt = ".zmbx"; // Default file extension
            dlg.Filter = "ZMBX (.zmbx)|*.zmbx"; // Filter files by extension
            string importDir = Path.GetFullPath(Path.Combine(LDrawFolders.RootFolder, @"..\Import"));
            dlg.InitialDirectory = importDir;
            if (dlg.ShowDialog() == true)
            {
                foreach (var filename in dlg.FileNames)
                {
                    MbxImport mbxImport = new MbxImport(filename);
                    mbxImport.WriteAll();
                }
            }
        }

        private void _RenderControl_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            vis.OnKeyDown(e);
        }

        private void _RenderControl_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            vis.OnKeyUp(e);
        }

        private void TreeView_SelectedINodeChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.OldValue != null)
                (e.OldValue as Topology.INode).IsSelected = false;
            if (e.NewValue != null)
                (e.NewValue as Topology.INode).IsSelected = true;
            vis.Part = selectedPart;
        }

        private void Eps_TextChanged(object sender, TextChangedEventArgs e)
        {
        }

        private void Eps_LostFocus(object sender, RoutedEventArgs e)
        {
            Eps.Epsilon = float.Parse((sender as TextBox).Text);
            OnSelectedItem(this.selectedItem);
        }

        private void Button_NodeNavClick(object sender, RoutedEventArgs e)
        {
            Button btn = (sender as Button);
            object dc = btn.DataContext;
            if (dc is Topology.BSPNode)
            {
                SelectBSPNode(dc as Topology.BSPNode);
            }
            else
            {
                if (dc is Topology.EdgePtr)
                    dc = (dc as Topology.EdgePtr).e;
                Vis_OnINodeSelected(sender, (dc as Topology.INode));
            }
        }

        private void BSPTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            Topology.BSPNode node = e.NewValue as Topology.BSPNode;
            if (node != null && node.nodeIdx == 0)
                node = null;
            vis.SelectedBSPNode = node;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedBSPNode"));

        }

        void SelectBSPNode(Topology.BSPNode node)
        {
            vis.SelectedBSPNode = node;

            if (node != null)
            {
                node.IsSelected = true;
                node.IsExpanded = true;
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedBSPNode"));
        }
        private void Goto_BSPNode_BtnClick(object sender, RoutedEventArgs e)
        {
            Topology.BSPNode node = (sender as Button).Content as Topology.BSPNode;

            vis.SelectedBSPNode = node;
            vis.SelectedBSPNode.IsExpanded = true;
            SelectBSPNode(node);
        }

        private void BSPFace_BtnClick(object sender, RoutedEventArgs e)
        {
            int faceidx = (int)(sender as Button).Content;
        }

        private void ShowPlaneNode_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button).DataContext is Topology.PortalFace)
            {
                Topology.PortalFace portalFace = (sender as Button).DataContext as Topology.PortalFace;
                vis.SelectedPortalFace = portalFace;
                Topology.PolygonClip.SetLogIdx(portalFace.PlaneNode.nodeIdx, true);
            }
            else if ((sender as Button).DataContext is Topology.BSPNode)
            {
                Topology.BSPNode node = (sender as Button).DataContext as Topology.BSPNode;
                Topology.PolygonClip.SetLogIdx(node.nodeIdx, true);
            }
            OnSelectedItem(this.selectedItem);
        }

        private void ShowPlanePolys_Click(object sender, RoutedEventArgs e)
        {
            Topology.PortalFace portalFace = (sender as Button).DataContext as Topology.PortalFace;
            polyLog.LogText = portalFace.GenPolyLog();
            vis.SelectedPortalFace = portalFace;
        }

        private void BSPNodeTextBtn_Click(object sender, RoutedEventArgs e)
        {
            int nodeIdx;
            if (int.TryParse(BspNodeTB.Text, out nodeIdx))
            {
                Topology.BSPNode node =
                    selectedPart.GetTopoMesh().bSPTree.Top.FromIdx(nodeIdx);
                selectedPart.GetTopoMesh().bSPTree.Top.IsExpanded = false;
                vis.SelectedBSPNode = node;
                vis.SelectedBSPNode.IsExpanded = true;
                SelectBSPNode(node);
            }
        }

        private void EdgeAddBSPPlane_Click(object sender, RoutedEventArgs e)
        {
            Topology.Edge edge = (sender as Button).DataContext as Topology.Edge;
            //selectedPart.GetTopoMesh().
        }

        private void FaceIdxTextBtn_Click(object sender, RoutedEventArgs e)
        {
            int faceIdx;
            if (int.TryParse(FaceIdxTB.Text, out faceIdx))
            {
                Topology.Face f = selectedPart.GetTopoMesh().faces.FirstOrDefault(f => f.idx == faceIdx);
                if (f != null)
                {
                    SelectINode(f, false);
                    vis.SelectFace(f);
                }
            }
        }
        private void ShowAllBSPBtn_Click(object sender, RoutedEventArgs e)
        {
        }

        private void HideAllBSPBtn_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Topology.BSPNode node =
                (sender as Button).DataContext as Topology.BSPNode;
            int traceNdext = node.Portal.TraceIdx + 1;
            var portals = selectedPart?.GetTopoMesh().bSPTree.GetLeafPortals();
            var nextPortal = portals.FirstOrDefault(p => p.TraceIdx == traceNdext);
            if (nextPortal != null)
            {
                vis.SelectedBSPNode = nextPortal.parentNode;
                SelectBSPNode(nextPortal.parentNode);
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            Topology.BSPNode node =
                (sender as Button).DataContext as Topology.BSPNode;
            int traceNdext = node.Portal.TraceIdx - 1;
            var portals = selectedPart?.GetTopoMesh().bSPTree.GetLeafPortals();
            var nextPortal = portals.FirstOrDefault(p => p.TraceIdx == traceNdext);
            if (nextPortal != null)
            {
                vis.SelectedBSPNode = nextPortal.parentNode;
                SelectBSPNode(nextPortal.parentNode);
            }
        }

        private void SubPartFilter_Click(object sender, RoutedEventArgs e)
        {
            List<string> parts = SelectedNode.IncludedInParts;
            var worldScale = SelectedNode.WorldScale;
            var allParts = LDrawFolders.AllParts;
            foreach (var ap in allParts)
            {
                ap.includedInFilter = false;
            }
            foreach (string p in parts)
            {
                LDrawFolders.Entry entry = LDrawFolders.GetEntry(p);
                if (entry != null)
                {
                    LDrawDatFile file = LDrawFolders.GetPart(entry);
                    var results = file.GetAllSubPartsOfType(SelectedNode.File.Name);
                    bool foundMatch = false;
                    foreach (var result in results)
                    {
                        Vector3 scl = result.mat.GetScale();
                        if (Eps.Eq(scl.X, worldScale.X) &&
                            Eps.Eq(scl.Z, worldScale.Z))
                        {
                            foundMatch = true;
                        }
                    }
                    entry.includedInFilter = foundMatch;
                }
            }
            LDrawFolders.FilterEnabled = true;
            FilteredCheckbox.IsChecked = true;            
        }

        private void FilteredCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            LDrawFolders.FilterEnabled = FilteredCheckbox.IsChecked == true;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("LDrawParts"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("LDrawGroups"));            
        }

        private void PartNumTB_LostFocus(object sender, RoutedEventArgs e)
        {
            string srchstr = (sender as TextBox).Text;
            LDrawFolders.SearchString = srchstr;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("LDrawParts"));
        }
    }

    public class TopoTemplateSelector : DataTemplateSelector
    {
        public DataTemplate EdgeTemplate { get; set; }
        public DataTemplate FaceTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is Topology.Edge)
                return EdgeTemplate;

            if (item is Topology.Face)
                return FaceTemplate;

            return null;
        }
    }

    public class BoolToBrush : IValueConverter
    {
        public Brush FalseBrush { get; set; }
        public Brush TrueBrush { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((targetType == typeof(Brush) || targetType == typeof(Object)))
            {
                bool nVal = (bool)value;
                return nVal ? TrueBrush : FalseBrush;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new ArgumentException();
        }
    }
}
