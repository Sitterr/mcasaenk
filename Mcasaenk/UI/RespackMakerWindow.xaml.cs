using Mcasaenk.Colormaping;
using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Shapes;

namespace Mcasaenk.UI {
    /// <summary>
    /// Interaction logic for RespackMakerWindow.xaml
    /// </summary>
    public partial class RespackMakerWindow : Window {
        private OperationMode mode;
        private object data;
        public RespackMakerWindow(OperationMode mode, object data) {
            InitializeComponent();

            colormap_editor.Init(ColormapEditorClosed);
            colormap_maker.Init(ColormapMakerClosed);

            this.mode = mode;
            this.data = data;

            this.Loaded += RespackMakerWindow_Loaded;

            colormap_editor.Visibility = Visibility.Collapsed;
            colormap_maker.Visibility = Visibility.Collapsed;
            txt_loading.Visibility = Visibility.Collapsed;
        }

        private async void RespackMakerWindow_Loaded(object sender, RoutedEventArgs e) {
            if(mode == OperationMode.ColormapEditor) {
                colormap_editor.SetUp(true, (string)data);
                colormap_editor.Visibility = Visibility.Visible;
            } else if(mode == OperationMode.ColormapMaker) {
                colormap_maker.SetUp();
                colormap_maker.Visibility = Visibility.Visible;         
            }
        }

        private void ColormapEditorClosed() {
            this.Close();
            GC.Collect(2, GCCollectionMode.Aggressive);
        }

        public string ColormapResult() {
            return Global.ReadName(colormap_editor.savepath);
        }

        private async void ColormapMakerClosed() {
            colormap_maker.Visibility= Visibility.Collapsed;

            txt_loading.Visibility = Visibility.Visible;
            RawColormap colormap = null;
            Options options = default;
            await Task.Run(() => {
                options = new Options();
                var reads = colormap_maker.GetResult().Select(r => ReadInterface.GetSuitable(r));
                colormap = ResourcepackColormapMaker.Make(reads.ToArray(), options);
                foreach(var r in reads) r.Dispose();
            });
            txt_loading.Visibility = Visibility.Collapsed;

            colormap_editor.Visibility = Visibility.Visible;
            colormap_editor.SetUp(colormap, options, "Generation successful!", new SolidColorBrush(Colors.Green), true);
        }
    }

    public enum OperationMode { ColormapEditor, ColormapMaker }
}
