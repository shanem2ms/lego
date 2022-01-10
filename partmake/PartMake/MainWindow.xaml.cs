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

        LDrawFolders.Entry selectedItem = null;
        LDrawDatFile selectedPart = null;
        string textFilter = "";
        PartVis vis = null;

        public string SelectedItemDesc { get { return selectedItem?.desc; } }
        public List<LDrawDatNode> CurrentPart { get => new List<LDrawDatNode>() { new LDrawDatNode { File = selectedPart } }; }

        public event PropertyChangedEventHandler PropertyChanged;

        public LDrawFolders.Entry SelectedItem
        {
            get { return selectedItem; }
            set
            {
                selectedItem = value; 
                OnSelectedItem(selectedItem);
                if (selectedItem != null && vis != null)
                    vis.Part = selectedItem;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItem"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItemDesc"));                
            }
        }

        public MainWindow()
        {
            LDrawFolders.SetRoot(@"c:\ldraw");
            this.DataContext = this;
            InitializeComponent();
            SelectedItem = LDrawFolders.GetEntry("4733.dat");
            vis = _RenderControl.Vis;
            if (selectedItem != null)
                vis.Part = selectedItem;
            //LDrawFolders.LoadAll();
        }


        void OnSelectedItem(LDrawFolders.Entry item)
        {
            if (item == null)
                return;
            selectedPart = LDrawFolders.GetPart(item);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentPart"));
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
            //threeD.Refresh();
        }
    }
}
