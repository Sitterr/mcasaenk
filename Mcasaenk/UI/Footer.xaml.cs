using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Mcasaenk.Colormaping;
using Mcasaenk.Rendering;

namespace Mcasaenk.UI {
    /// <summary>
    /// Interaction logic for Footer.xaml
    /// </summary>
    public partial class Footer : UserControl {
        private Brush lblue, lred;
        public Footer() {
            InitializeComponent();

            this.lblue = this.FindResource("LIGHT_BLUE_B") as Brush;
            this.lred = this.FindResource("LIGHT_RED_B") as Brush;

        }

        public void Refresh() {
            gr_blockinfo.Visibility = Global.Settings.BLOCKINFO ? Visibility.Visible : Visibility.Collapsed;
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


        public double ShadeTiles {
            get => Convert.ToDouble(txt_shadetiles.Text);
            set => txt_shadetiles.Text = value.ToString();
        }
        public double ShadeFrames {
            get => Convert.ToDouble(txt_shadeframes.Text);
            set => txt_shadeframes.Text = value.ToString();
        }



        public void SetCursorInfo(Point2i globalPos, GenDataTileMap tileMap) {
            txt_x.Text = globalPos.X.ToString();
            txt_z.Text = globalPos.Z.ToString();

            var tile = tileMap?.GetTile(new Point2i((int)Math.Floor(globalPos.X / 512.0), (int)Math.Floor(globalPos.Z / 512.0)));
            int i = Global.Coord.absMod(globalPos.Z, 512) * 512 + Global.Coord.absMod(globalPos.X, 512);
            if(tile != null) {
                bool info = false;
                foreach(var col in tile.columns) {
                    if(col.ContainsInfo(i) == false) continue;
                    info = true;

                    txt_y.Text = (col.heights[i] + Global.Settings.MINY).ToString();

                    bool depth = col == tile.depthColumn && (tile.depthColumn.depths15_lightfrombottom1 != null ? tile.depthColumn.depths15_lightfrombottom1[i] : -1) > 0;
                    //window.footer.Y = tile.genData.isShade(i);
                    if(depth) {
                        sep_y.Text = "/";
                        txt_ty.Text = (col.heights[i] + Global.Settings.MINY - tile.depthColumn.depths15_lightfrombottom1[i]).ToString();
                    } else {
                        sep_y.Text = "";
                        txt_ty.Text = "";
                    }

                    if(Global.Settings.BLOCKINFO) {
                        {
                            SetStringText(Global.App.Colormap.Block.GetName(col.blockIds[i]), txt_block);
                            if(depth) {
                                sep_block.Text = "/";
                                SetStringText(Global.App.Colormap.Block.GetName(BlockManager.depth), txt_block2);
                            } else {
                                sep_block.Text = "";
                                SetStringText("", txt_block2);
                            }

                            SetStringText(Global.App.Colormap.Biome.GetName(col.BiomeId(i)), txt_biome);
                        }
                    }

                    break;
                }
                if(!info) SetEmpty();
            } else SetEmpty();
        }

        private void SetEmpty() {
            txt_y.Text = "";
            sep_y.Text = "";
            txt_ty.Text = "";

            SetStringText("", txt_block);
            sep_block.Text = "";
            SetStringText("_void_", txt_block2);
            SetStringText("_void_", txt_biome);
        }

        private void SetStringText(string value, Run run) {
            bool italic = value.StartsWith('_') && value.EndsWith('_');
            if(italic) value = value.Substring(1, value.Length - 2);
            run.Text = value;
            run.FontStyle = italic ? FontStyles.Italic : FontStyles.Normal;
            run.Foreground = italic ? lred : lblue;
        }
    }
}
