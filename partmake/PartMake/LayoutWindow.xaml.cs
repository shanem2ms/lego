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
using System.ComponentModel;

namespace partmake
{
    /// <summary>
    /// Interaction logic for LayoutWindow.xaml
    /// </summary>
    public partial class LayoutWindow : UserControl, INotifyPropertyChanged
    {
        public IEnumerable<string> CacheGroups { get => LDrawFolders.LDrawGroups; }

        public string FilterText { get; set; }

        public IEnumerable<LDrawFolders.CacheItem> AllItems { get => LDrawFolders.CacheItems.Values; }
        public string SelectedType
        {
            get => LDrawFolders.SelectedType;
            set
            {
                LDrawFolders.SelectedType = value;
            }
        }

        public LayoutWindow()
        {
            this.DataContext = this;
            InitializeComponent();

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CacheGroups"));
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

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}
