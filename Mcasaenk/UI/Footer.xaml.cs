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

            @interface = new FooterInterface(this.FindResource("LIGHT_BLUE_B") as Brush, this.FindResource("LIGHT_RED_B") as Brush) { 
                txt_fps = txt_fps, 

                txt_redraw = txt_redraw,
                txt_gendraw = txt_gendraw,

                txt_shadetiles = txt_shadetiles, 
                txt_shadeframes = txt_shadeframes,

                txt_x = txt_x,
                txt_y = txt_y,
                txt_ty = txt_ty,
                txt_z = txt_z,

                txt_block = txt_block,
                txt_biome = txt_biome,
            };

        }

        public void Init() { }
    }

    public class FooterInterface {

        public Run txt_fps, txt_redraw, txt_gendraw, txt_shadetiles, txt_shadeframes, txt_x, txt_z, txt_y, txt_ty, txt_block, txt_biome;

        private Brush lblue, lred;
        public FooterInterface(Brush lblue, Brush lred) { 
            this.lblue = lblue;
            this.lred = lred;
        }

        public int Fps {
            get => Convert.ToInt16(txt_fps.Text);
            set => txt_fps.Text = value.ToString();
        }


        public long DrawTime {
            get => Convert.ToInt32(txt_redraw.Text);
            set => txt_redraw.Text = value.ToString();
        }
        public long GenerateTime {
            get => Convert.ToInt32(txt_gendraw.Text);
            set => txt_gendraw.Text = value.ToString();
        }


        public int ShadeTiles {
            get => Convert.ToInt16(txt_shadetiles.Text);
            set => txt_shadetiles.Text = value.ToString();
        }
        public int ShadeFrames {
            get => Convert.ToInt16(txt_shadeframes.Text);
            set => txt_shadeframes.Text = value.ToString();
        }


        public int X {
            get => Convert.ToInt32(txt_x.Text);
            set => txt_x.Text = value.ToString();
        }
        public int Y {
            get => Convert.ToInt32(txt_y.Text);
            set => txt_y.Text = value.ToString();
        }
        public int Y_Terrain {
            get => Convert.ToInt32(txt_ty.Text);
            set => txt_ty.Text = value.ToString();
        }
        public int Z {
            get => Convert.ToInt32(txt_z.Text);
            set => txt_z.Text = value.ToString();
        }

        public string Block {
            get => txt_block.Text;
            set {
                bool unknown = value == "_unknown_";
                if(unknown) value = "unknown block";
                txt_block.Text = value; 
                txt_block.FontStyle = unknown ? FontStyles.Italic : FontStyles.Normal;
                txt_block.Foreground = unknown ? lred : lblue;
            }
        }
        public string Biome {
            get => txt_biome.Text;
            set {
                bool unknown = value == "_unknown_";
                if(unknown) value = "unknown biome";
                txt_biome.Text = value; 
                txt_biome.FontStyle = unknown ? FontStyles.Italic : FontStyles.Normal;
                txt_biome.Foreground = unknown ? lred : lblue;
            }
        }
    }
}
