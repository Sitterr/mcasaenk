using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Mcasaenk.UI {
    public class FooterInterface {

        private Run txt_queue, txt_fps, txt_region, txt_redraw;

        public FooterInterface(Run txt_queue, Run txt_fps, Run txt_region, Run txt_redraw) { 
            this.txt_queue = txt_queue;
            this.txt_fps = txt_fps;
            this.txt_region = txt_region;
            this.txt_redraw = txt_redraw;
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

        public Point2i Region {
            set {
                txt_region.Text = value.ToString();
            }
        }

    }
}
