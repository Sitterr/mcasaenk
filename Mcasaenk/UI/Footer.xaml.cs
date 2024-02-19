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

namespace Mcasaenk.UI {
    /// <summary>
    /// Interaction logic for Footer.xaml
    /// </summary>
    public partial class Footer : UserControl {
        public FooterInterface @interface;
        public Footer() {
            InitializeComponent();

            @interface = new FooterInterface(txt_queue, txt_fps, txt_region, txt_redraw, txt_shadetiles, txt_shadeframes);
        }

        public void Init() { }
    }
}
