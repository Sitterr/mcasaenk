using Mcasaenk.Rendering;
using Mcasaenk.Shade3d;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
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
    /// Interaction logic for LeftSettingsMenu.xaml
    /// </summary>
    public partial class LeftSettingsMenu : UserControl
    {
        EButton[] tabs;
        FrameworkElement[] contents;
        public LeftSettingsMenu()
        {
            InitializeComponent();

            upd_meth_link.Click += (o, e) => {
                var window = new UpdateMethodClarifyWindow();
                window.Owner = Window.GetWindow(this);
                window.ShowDialog();
            };
            btn_change.Click += (o, e) => {
                Global.App.Settings.SetFromBack();
            };
            btn_undo.Click += (o, e) => {
                Global.App.Settings.Reset();
            };


            tabs = new[] { tab_general, tab_shades, tab_color };
            contents = new[] { cont_general, cont_shades, cont_color };

            bool loaded = false;
            this.Loaded += (o, e) => {
                if(loaded) return;
                loaded = true;
                tabs[0].Margin = new Thickness(0, 0, 0, 0);
                for(int _i = 0; _i < tabs.Length; _i++) {
                    int i = _i;
                    var tab = tabs[i];
                    var content = contents[i];
                    tab.Click += (o, e) => {
                        for(int j = 0; j < tabs.Length; j++) {
                            tabs[j].IsActive = i == j;
                            contents[j].Visibility = i == j ? Visibility.Visible : Visibility.Collapsed;
                            if(i != j) {
                                tabs[j].BorderThickness = new Thickness(0, 0, 0, 1);
                            }
                        }
                        if(i == 0) {
                            tabs[i].BorderThickness = new Thickness(1, 1, 1, 0);
                            tabs[i].Padding = new Thickness(0, 0, 0, 1);

                            tabs[i + 1].BorderThickness = new Thickness(1, 0, 0, 1);


                            scroll.HorizontalContentAlignment = HorizontalAlignment.Right;
                        } else if(i == tabs.Length - 1) {
                            tabs[i].BorderThickness = new Thickness(1, 1, 1, 0);


                            scroll.HorizontalContentAlignment = HorizontalAlignment.Left;
                        } else {
                            tabs[i].BorderThickness = new Thickness(1, 1, 1, 0);
                            tabs[i].Padding = new Thickness(0, 0, 0, 1);

                            tabs[i + 1].BorderThickness = new Thickness(1, 0, 0, 1);


                            scroll.HorizontalContentAlignment = HorizontalAlignment.Right;
                        }
                    };

                    if(i == 0) {
                        tabs[i].border.CornerRadius = new CornerRadius(3, 0, 0, 0);
                    } else if(i == tabs.Length - 1) {
                        tabs[i].border.CornerRadius = new CornerRadius(0, 3, 0, 0);
                    } else {
                        tabs[i].border.CornerRadius = new CornerRadius(0, 0, 0, 0);
                    }
                }

                tab_general.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));


                btn_undo.Margin = new Thickness(btn_undo.Margin.Left + btn_change.ActualWidth + btn_undo.ActualWidth + 20, btn_undo.Margin.Top, btn_undo.Margin.Right, btn_undo.Margin.Bottom);
            };
        }

        public void OnActive() {

        }

        private void OnResetBasis(object sender, RoutedEventArgs e) {
            Global.ViewModel.AllColormaps = null;
        }

        
        public void SetUpTintGrid(Colormap colormap) {
            tintGrid.RowDefinitions.Clear();
            tintGrid.Children.Clear();

            if(colormap == null) return;

            int i = 0;
            foreach(var tint in colormap.GetTints()) {
                if(i % 2 == 0) {
                    tintGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                    tintGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(2) });
                    tintGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto, MinHeight = 10 });
                    tintGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(40) });
                }

                {
                    var dockPanel = new DockPanel();
                    Grid.SetRow(dockPanel, (i / 2) * 4);
                    Grid.SetColumn(dockPanel, (i % 2) * 2);

                    dockPanel.Children.Add(new TextBlock() { Text = tint.name.Split('.')[0] });
                    var tintEnable = new ToggleButton { Margin = new Thickness(10, 0, 0, 0) };
                    var toggleBinding = new Binding("On") {
                        Source = tint.settings // Assuming 'Global.Settings' is correctly defined
                    };
                    tintEnable.SetBinding(ToggleButton.IsCheckedProperty, toggleBinding);
                    dockPanel.Children.Add(tintEnable);

                    var txtRaduis = new TextBlock() { HorizontalAlignment = HorizontalAlignment.Right };
                    txtRaduis.SetBinding(TextBlock.IsEnabledProperty, new Binding("On") { Source = tint.settings });
                    MultiBinding txtMultBinding = new MultiBinding { StringFormat = "{0}x{1}" };
                    txtMultBinding.Bindings.Add(new Binding("Blend") { Source = tint.settings });
                    txtMultBinding.Bindings.Add(new Binding("Blend") { Source = tint.settings });
                    txtRaduis.SetBinding(TextBlock.TextProperty, txtMultBinding);
                    dockPanel.Children.Add(txtRaduis);

                    tintGrid.Children.Add(dockPanel);


                    var slider = new Slider() {
                        IsSnapToTickEnabled = true,
                        Minimum = 1, Maximum = 33, TickFrequency = 2
                    };
                    Grid.SetRow(slider, (i / 2) * 4 + 2);
                    Grid.SetColumn(slider, (i % 2) * 2);
                    slider.SetBinding(Slider.IsEnabledProperty, new Binding("On") { Source = tint.settings });
                    slider.SetBinding(Slider.ValueProperty, new Binding("Blend") { Source = tint.settings });

                    tintGrid.Children.Add(slider);
                }

                i++;
            }

            if(tintGrid.RowDefinitions.Count > 0) tintGrid.RowDefinitions.RemoveAt(tintGrid.RowDefinitions.Count - 1);
        }
    }
}
