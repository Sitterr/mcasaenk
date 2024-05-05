using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Mcasaenk.UI {
    /// <summary>
    /// Interaction logic for Rad.xaml
    /// </summary>
    public partial class Rad : UserControl {
        public Rad() {
            InitializeComponent();

            btn_pre.Clicked += (o, e) => {
                RecalcSelected(btn_pre);
            };
            btn_frame.Click += (o, e) => {
                RecalcSelected(btn_frame);
            };
            btn_custom.Click += (o, e) => {
                RecalcSelected(btn_custom);
            };


            btn_frame.MouseEnter += (o, e) => {
                if(selectedc == btn_frame) return;
                btn_frame.Background = this.TryFindResource("HOVER") as Brush;
            };
            btn_frame.MouseLeave += (o, e) => {
                if(selectedc == btn_frame) {
                    //btn_frame.Background = this.TryFindResource("PRESS") as Brush;
                } else {
                    btn_frame.Background = transp;
                }
            };
            btn_custom.MouseEnter += (o, e) => {
                if(selectedc == btn_custom) return;
                btn_custom.Background = this.TryFindResource("HOVER") as Brush;
            };
            btn_custom.MouseLeave += (o, e) => {
                if(selectedc == btn_custom) {
                    //btn_custom.Background = this.TryFindResource("PRESS") as Brush;
                } else {
                    btn_custom.Background = transp;
                }
            };
        }


        private Resolution selected;
        private Control selectedc;

        public Resolution GetResolution() {
            return selected;
        }

        Brush transp = new SolidColorBrush(Colors.Transparent);
        private void RecalcSelected(Control control) {
            if(control != null) {
                btn_pre.IsEnabled = control == btn_pre;
                btn_frame.IsEnabled = control == btn_frame;
                btn_custom.IsEnabled = control == btn_custom;
            } else {
                btn_pre.IsEnabled = true;
                btn_frame.IsEnabled = true;
                btn_custom.IsEnabled = true;
            }

            //if(control != null) { 
            if(control == btn_frame) btn_frame.Background = this.TryFindResource("PRESS") as Brush;
            else btn_frame.Background = transp;

            if(control == btn_custom) btn_custom.Background = this.TryFindResource("PRESS") as Brush;
            else btn_custom.Background = transp;

            if(control == btn_pre) btn_pre.Background = this.TryFindResource("PRESS") as Brush;
            else btn_pre.Background = transp;
            //}

            if(control is ToggleButton tg) {
                selected = (Resolution)((ContentControl)(tg.Content)).DataContext;
            } else if(control is AButton ab) {
                selected = (Resolution)(ab.SelectedItem);
            }
            selectedc = control;
        }

        public void Reset() {
            RecalcSelected(null);
            selected = null;
            btn_pre.clicked = false;
        }
    }


    class AButton : ComboBox {
        public bool clicked = false;
        public bool setup = true;
        Brush transp = new SolidColorBrush(Colors.Transparent);
        public AButton() {
            this.IsMouseDirectlyOverChanged += (o, e) => this.IsDropDownOpen = false;
            this.MouseEnter += (o, e) => this.IsDropDownOpen = true;

            this.MouseUp += (o, e) => {
                if(clicked) return;
                clicked = true;
                Clicked(this, EventArgs.Empty);
                this.IsDropDownOpen = false;
                //this.Background = this.TryFindResource("PRESS") as Brush;
            };
            this.DropDownOpened += (o, e) => {
                if(clicked) {
                    this.IsDropDownOpen = false;
                    return;
                }
                this.Background = this.TryFindResource("HOVER") as Brush;
            };
            this.SelectionChanged += (o, e) => {
                if(setup) {
                    setup = false;
                    return;
                }
                clicked = true;
                Clicked(this, EventArgs.Empty);
                //this.Background = this.TryFindResource("PRESS") as Brush;
            };
            this.DropDownClosed += (o, e) => {
                if(clicked) {
                    this.Background = this.TryFindResource("PRESS") as Brush;
                } else {
                    this.Background = transp;
                }
            };
        }

        public event EventHandler Clicked;
    }
}
