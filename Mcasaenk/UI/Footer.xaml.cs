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

            @interface = new FooterInterface(txt_queue, txt_fps, txt_region, txt_redraw, txt_shadetiles, txt_shadeframes, txt_biome);
        }

        public void Init() { }
    }


    public class FooterInterface {

        private Run txt_queue, txt_fps, txt_region, txt_redraw, txt_shadetiles, txt_shadeframes, txt_biome;

        public FooterInterface(Run txt_queue, Run txt_fps, Run txt_region, Run txt_redraw, Run txt_shadetiles, Run txt_shadeframes, Run txt_biome) {
            this.txt_queue = txt_queue;
            this.txt_fps = txt_fps;
            this.txt_region = txt_region;
            this.txt_redraw = txt_redraw;
            this.txt_shadetiles = txt_shadetiles;
            this.txt_shadeframes = txt_shadeframes;
            this.txt_biome = txt_biome;
        }

        public int RegionQueue {
            get {
                return Convert.ToInt16(txt_queue.Text);
            }
            set {
                txt_queue.Text = value.ToString();
            }
        }

        public int Fps {
            get {
                return Convert.ToInt16(txt_fps.Text);
            }
            set {
                txt_fps.Text = value.ToString();
            }
        }

        public long HardDraw {
            get {
                return Convert.ToInt32(txt_redraw.Text);
            }
            set {
                txt_redraw.Text = value.ToString();
            }
        }
        public string HardDraw_Raw {
            get {
                return txt_redraw.Text;
            }
            set {
                txt_redraw.Text = value;
            }
        }

        public Point2i Region {
            set {
                txt_region.Text = value.ToString();
            }
        }

        public int ShadeTiles {
            get {
                return Convert.ToInt16(txt_shadetiles.Text);
            }
            set {
                txt_shadetiles.Text = value.ToString();
            }
        }

        public int ShadeFrames {
            get {
                return Convert.ToInt16(txt_shadeframes.Text);
            }
            set {
                txt_shadeframes.Text = value.ToString();
            }
        }

        public string Biome { 
            get { return txt_biome.Text; }
            set {
                txt_biome.Text = value;
            }
        }
    }
}
