using Mcasaenk.UI.Canvas;
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
        Control[] slots;
        public Rad() {
            InitializeComponent();

            slots = [slot1_topdown, slot2_static, slot3_static];
            foreach(var slot in slots) {
                if(slot is Button static_slot) {
                    static_slot.Click += (o, e) => RecalcSelected(slot);

                    static_slot.MouseEnter += (o, e) => {
                        if(selectedc == static_slot) return;
                        static_slot.Background = this.TryFindResource("HOVER") as Brush;
                    };
                    static_slot.MouseLeave += (o, e) => {
                        if(selectedc == static_slot) {
                            //btn_custom.Background = this.TryFindResource("PRESS") as Brush;
                        } else {
                            static_slot.Background = transp;
                        }
                    };

                } else if(slot is AButton topdown_slot) topdown_slot.Clicked += (o, e) => RecalcSelected(slot);
            }
        }


        public void ShowSlot3(bool visible) {
            if(visible) {
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1) });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(85) });
            } else if(grid.ColumnDefinitions.Count == 5) {
                grid.ColumnDefinitions.RemoveAt(3);
                grid.ColumnDefinitions.RemoveAt(3);
            }

            sl2_b.BorderThickness = visible ? new Thickness(0, 1, 0, 1) : new Thickness(0, 1, 1, 1);

            sl2sl3.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            sl3_b.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }


        private Resolution selected;
        private Control selectedc;


        private Action onSelect;
        public void SetCallback(Action onSelect) {
            this.onSelect = onSelect;
        }

        public Resolution GetResolution() {
            return selected;
        }

        Brush transp = new SolidColorBrush(Colors.Transparent);
        private void RecalcSelected(Control control) {
            if(control != null) {
                foreach(var slot in slots) slot.IsEnabled = control == slot;
            } else {
                foreach(var slot in slots) slot.IsEnabled = true;
            }

            foreach(var slot in slots) {
                if(control == slot) slot.Background = this.TryFindResource("PRESS") as Brush;
                else slot.Background = transp;
            }

            if(control is Button b) {
                selected = (Resolution)((ContentControl)(b.Content)).Content;
            } else if(control is AButton ab) {
                selected = (Resolution)(ab.SelectedItem);
            }
            selectedc = control;

            onSelect();
        }

        public void Reset(bool tilemap) {
            RecalcSelected(null);

            selected = null;
            onSelect();

            foreach(var slot in slots) {
                if(slot is AButton slot_topdown) slot_topdown.clicked = false;
            }
        }

        public void PreDefined(Resolution[] resolutions) {
            if(resolutions == null) resolutions = [];
            if(this.IsLoaded == false) return;
            var screenres = Resolution.CurrentResolution(this);
            Resolution.screen.X = screenres.w; Resolution.screen.Y = screenres.h;

            var list = resolutions.ToList();
            list.Insert(0, Resolution.screen);
            slot1_topdown.ItemsSource = list;
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
