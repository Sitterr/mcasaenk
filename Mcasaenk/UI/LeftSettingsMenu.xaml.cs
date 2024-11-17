using Mcasaenk.Colormaping;
using Mcasaenk.Shade3d;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Globalization;
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
    public partial class LeftSettingsMenu : UserControl {
        EButton[] tabs;
        FrameworkElement[] contents;

        Brush light_blue_b;
        public LeftSettingsMenu() {
            InitializeComponent();

            upd_meth_link.Click += (o, e) => {
                // depr
            };
            btn_change.Click += (o, e) => {
                Global.App.Settings.SetFromBack();
            };
            btn_undo.Click += (o, e) => {
                Global.App.Settings.Reset();
            };

            light_blue_b = this.TryFindResource("LIGHT_BLUE_B") as Brush;

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

        private void OnCreateColormap(object sender, RoutedEventArgs e) {
            var w = new RespackMakerWindow(OperationMode.ColormapMaker, null);
            w.ShowDialog();
            string res = w.ColormapResult();
            if(res != "" && res != null) {
                Global.ViewModel.AllColormaps = null;
                Global.Settings.ColorMapping = w.ColormapResult();
            }
        }

        private void ViewColormap(object sender, RoutedEventArgs e) {
            new RespackMakerWindow(OperationMode.ColormapEditor, Settings.ColormapToPath(Global.Settings.ColorMapping)).ShowDialog();
        }


        class IHateWPF_YVarianceMultivalueConverter : IMultiValueConverter {
            public object Convert(object[] value, Type targetType, object parameter, CultureInfo culture) {
                if(value.Length == 0) return -1;
                return value[0];
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
                return Enumerable.Repeat(value, targetTypes.Length).ToArray();
            }
        }
        class IHateWPF_YVarianceTextConverter : IValueConverter {
            public object Convert(object _value, Type targetType, object parameter, CultureInfo culture) {
                if(_value is double value) {
                    if(value == 0) return "off";
                    else if(value == 1) return "default";
                    else return $"x{Math.Round(value, 1)}";
                }
                return _value;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotImplementedException();
            }
        }
        class IHateWPF_DefBiomeConverter(Colormap colormap) : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return colormap.Biome.GetName((ushort)value).simplifyminecraftname();
            }
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                return colormap.Biome.GetId(((string)value).minecraftname());
            }
        }
        class IHateWPF_ContraBool : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return !((bool)value);
            }
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotImplementedException();
            }
        }

        public void SetUpColormapSettings(Colormap colormap) {
            tintGrid.RowDefinitions.Clear();
            tintGrid.Children.Clear();

            colormapNotLoaded.Visibility = colormap != null ? Visibility.Collapsed : Visibility.Visible;
            colormapSettingsCont.Visibility = colormap != null ? Visibility.Visible : Visibility.Collapsed;

            if(colormap == null) {
                return;
            } else {
                var yvarvanillabind = new MultiBinding() { Converter = new IHateWPF_YVarianceMultivalueConverter() };
                MultiBinding yvarvanillaisenabled = new MultiBinding() { Converter = new BitOrConverter() }, defbiomeisenabled = new MultiBinding() { Converter = new BitOrConverter() };
                foreach(var tint in colormap.TintManager.ELEMENTS) {
                    if(tint is VanillaDynTint) {
                        yvarvanillabind.Bindings.Add(new Binding("TemperatureVariation") { Source = tint, Mode = BindingMode.TwoWay });
                        yvarvanillaisenabled.Bindings.Add(new Binding("On") { Source = tint, Mode = BindingMode.OneWay });
                    }

                    if(tint is DynamicTint) {
                        defbiomeisenabled.Bindings.Add(new Binding("On") { Source = tint, Mode = BindingMode.OneWay, Converter = new IHateWPF_ContraBool() });
                    }
                }
                text_yvarvanilla.SetBinding(TextBlock.IsEnabledProperty, yvarvanillaisenabled);
                text_yvarvanilla.SetBinding(TextBlock.TextProperty, new Binding() { Source = slider_yvarvanilla, Path = new PropertyPath("Value"), Converter = new IHateWPF_YVarianceTextConverter() });


                slider_yvarvanilla.Visibility = yvarvanillabind.Bindings.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                label_yvarvanilla.Visibility = slider_yvarvanilla.Visibility;
                text_yvarvanilla.Visibility = slider_yvarvanilla.Visibility;
                slider_yvarvanilla.SetBinding(Slider.ValueProperty, yvarvanillabind);
                slider_yvarvanilla.SetBinding(Slider.IsEnabledProperty, yvarvanillaisenabled);
                label_yvarvanilla.SetBinding(Label.IsEnabledProperty, yvarvanillaisenabled);


                combo_defbiome.ItemsSource = colormap.Biome.GetAllNames().Select(l => l.simplifyminecraftname());
                combo_defbiome.SetBinding(ComboBox.SelectedValueProperty, new Binding("DEFBIOME") { Source = Global.Settings, Converter = new IHateWPF_DefBiomeConverter(colormap) });
                //combo_defbiome.SetBinding(ComboBox.IsEnabledProperty, defbiomeisenabled);
                //label_defbiome.SetBinding(Label.IsEnabledProperty, defbiomeisenabled);

                macroGrid.Visibility = colormap.TintManager.ELEMENTS.Any(t => t is DynamicTint) ? Visibility.Visible : Visibility.Collapsed;
            }

            {
                int i = 0;
                foreach(var tint in colormap.TintManager.ELEMENTS) {
                    if(tint is not DynamicTint) continue;

                    if(i % 2 == 0) {
                        tintGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                        tintGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(2) });
                        tintGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto, MinHeight = 10 });
                        tintGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(25) });
                    }

                    if(tint is DynamicTint dtint) {
                        var dockPanel = new DockPanel();
                        Grid.SetRow(dockPanel, (i / 2) * 4);
                        Grid.SetColumn(dockPanel, (i % 2) * 2);

                        var txtname = new TextBlock();
                        var tintmeta = TintMeta.GetFormat(tint.GetType());
                        txtname.Inlines.Add(new Run() { Text = dtint.name + "/" });
                        txtname.Inlines.Add(new Run() { Text = tintmeta.kurzformat, FontStyle = FontStyles.Italic, FontSize = 12, Foreground = light_blue_b });
                        txtname.Inlines.Add(new Run() { Text = "/" });
                        dockPanel.Children.Add(txtname);

                        var tintEnable = new ToggleButton { Margin = new Thickness(10, 0, 0, 0) };
                        var toggleBinding = new Binding("On") {
                            Source = dtint // Assuming 'Global.Settings' is correctly defined
                        };
                        tintEnable.SetBinding(ToggleButton.IsCheckedProperty, toggleBinding);
                        dockPanel.Children.Add(tintEnable);

                        var txtRaduis = new TextBlock() { HorizontalAlignment = HorizontalAlignment.Right };
                        txtRaduis.SetBinding(TextBlock.IsEnabledProperty, new Binding("On") { Source = dtint });
                        MultiBinding txtMultBinding = new MultiBinding { StringFormat = "{0}x{0}" };
                        txtMultBinding.Bindings.Add(new Binding("Blend") { Source = dtint });
                        txtMultBinding.Bindings.Add(new Binding("Blend") { Source = dtint });
                        txtRaduis.SetBinding(TextBlock.TextProperty, txtMultBinding);
                        dockPanel.Children.Add(txtRaduis);

                        tintGrid.Children.Add(dockPanel);

                        var slider = new Slider() {
                            IsSnapToTickEnabled = true,
                            Minimum = 1,
                            Maximum = tintmeta.maxblend,
                            TickFrequency = 4
                        };
                        Grid.SetRow(slider, (i / 2) * 4 + 2);
                        Grid.SetColumn(slider, (i % 2) * 2);
                        slider.SetBinding(Slider.IsEnabledProperty, new Binding("On") { Source = dtint });
                        slider.SetBinding(Slider.ValueProperty, new Binding("Blend") { Source = dtint });

                        tintGrid.Children.Add(slider);
                    }

                    i++;
                }
                if(tintGrid.RowDefinitions.Count > 0) tintGrid.RowDefinitions.RemoveAt(tintGrid.RowDefinitions.Count - 1);
                tintSettings.Visibility = tintGrid.RowDefinitions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }

        }
    }
}
