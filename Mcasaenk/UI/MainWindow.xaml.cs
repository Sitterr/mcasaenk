using System.Data.Common;
using System.Diagnostics;
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

        public FooterInterface footer;

        public MainWindow() {
            InitializeComponent();

            footer = footerControl.@interface;
            canvasControl.Init(this);        
            footerControl.Init();

            MouseHook.Start();
            MouseHook.MouseMove += (a, b) => {
                if(canvasControl.mousedown) {
                    canvasControl.OnMouseMove(canvasControl.GetRelativeMouse(b));
                }
            };
            MouseHook.MouseDown += (a, b) => {
            };
            MouseHook.MouseUp += (a, b) => {
                if(canvasControl.mousedown) {
                    canvasControl.OnMouseUp(a, null);
                }
            };

        }
    }
}