using System.Data.Common;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;

namespace Mcasaenk.UI {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();

            this.KeyUp += MainWindow_KeyUp;

            canvas.Init(footer.@interface);
            footer.Init();
        }

        private void MainWindow_KeyUp(object sender, KeyEventArgs e) {
            //canvas.OnKeyUp(sender, e);
        }
    }
}