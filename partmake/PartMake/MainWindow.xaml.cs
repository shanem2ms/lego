using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Linq;
using System.ComponentModel;

namespace partmake
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public List<LDrawFolders.Entry> LDrawParts { get
            {
                return LDrawFolders.LDrawParts;
            } }

        public IEnumerable<string> LDrawGroups { get => LDrawFolders.LDrawGroups; }

        LDrawFolders.Entry selectedItem = null;
        LDrawDatFile selectedPart = null;
        string textFilter = "";
        PartVis vis = null;

        public string SelectedItemDesc { get { return selectedItem?.desc; } }
        public List<LDrawDatNode> CurrentPart { get => new List<LDrawDatNode>() { new LDrawDatNode { File = selectedPart } }; }

        public event PropertyChangedEventHandler PropertyChanged;

        LDrawDatNode selectedNode = null;
        public LDrawDatNode SelectedNode => selectedNode;
        public Topology.INode SelectedINode { get; set; }
        public string Log => selectedPart?.GetTopoMesh().LogString;

        public Topology.BSPNode SelectedBSPNode => vis?.SelectedBSPNode;
        public List<Topology.BSPNode> BSPNodes =>
            new List<Topology.BSPNode>() {selectedPart?.GetTopoMesh().bSPTree.Top };

        public Topology.Settings TopoSettings { get; } = new Topology.Settings();
        public string SelectedType { get => LDrawFolders.SelectedType; 
            set { LDrawFolders.SelectedType = value;
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
            }
        }

        public MainWindow()
        {
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
            Eps.Text = Topology.Mesh.Epsilon.ToString();
            vis.OnINodeSelected += Vis_OnINodeSelected;
            vis.OnBSPNodeSelected += Vis_OnBSPNodeSelected;
        }
      
        private void Settings_SettingsChanged(object sender, EventArgs e)
        {
            selectedPart.ClearTopoMesh();
            vis.Part = selectedPart;
        }

        private void Vis_OnBSPNodeSelected(object sender, Topology.BSPNode e)
        {
            SelectBSPNode(e);
        }

        private void Vis_OnINodeSelected(object sender, Topology.INode e)
        {
            if (SelectedINode != null)
                SelectedINode.IsSelected = false;
            SelectedINode = e;
            if (SelectedINode != null)
                SelectedINode.IsSelected = true;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedINode"));
        }

        void OnSelectedItem(LDrawFolders.Entry item)
        {
            if (item == null)
                return;
            selectedPart = LDrawFolders.GetPart(item);            
            if (vis != null)
                vis.Part = selectedPart;
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
            vis.Part = selectedPart;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            LDrawFolders.WriteAll();
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
            Topology.Mesh.Epsilon = float.Parse((sender as TextBox).Text);
            OnSelectedItem(this.selectedItem);
        }

        private void Button_NodeNavClick(object sender, RoutedEventArgs e)
        {
            Button btn = (sender as Button);
            object dc = btn.DataContext;
            if (dc is Topology.EdgePtr)
                dc = (dc as Topology.EdgePtr).e;
            Vis_OnINodeSelected(sender, (dc as Topology.INode));
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

                while (node != null)
                {
                    node.IsExpanded = true;
                    node = node.parent;
                }
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedBSPNode"));
        }
        private void Goto_BSPNode_BtnClick(object sender, RoutedEventArgs e)
        {
            Topology.BSPNode node = (sender as Button).Content as Topology.BSPNode;
            
            vis.SelectedBSPNode = node;
            SelectBSPNode(node);           
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
}
