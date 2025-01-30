using Mcasaenk.Colormaping;
using Mcasaenk.Shade3d;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
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
using static System.Net.WebRequestMethods;

namespace Mcasaenk.UI {
    /// <summary>
    /// Interaction logic for LeftSettingsMenu.xaml
    /// </summary>
    public partial class LeftSettingsMenu : UserControl {
        EButton[] tabs;
        FrameworkElement[] contents;

        Brush light_blue_b;

        bool loaded = false;
        public LeftSettingsMenu() {
            InitializeComponent();

            generalfilter_header.TextBlock.Text = "Invisible filter";
            generalfilter_header.Click += (o, e) => {
                var colormap = Global.App?.Colormap;
                if(colormap == null) return;

                var blocks = colormap.Block.All().Select(n => {
                    Filter blockfilter = colormap.FilterManager.Elements.FirstOrDefault(t => t.Blocks.Contains(n.Key));/* = colormap.TintManager.GetBlockVal(n.Key);*/
                    BinaryBlockGroupWindow.Group group = BinaryBlockGroupWindow.Group.Def;
                    if(blockfilter == colormap.FilterManager.Invis) group = BinaryBlockGroupWindow.Group.AlwaysThis;
                    //else if(blockfilter != null && blockfilter != colormap.FilterManager.Default) group = BinaryBlockGroupWindow.Group.Other;
                    
                    return (n.Value, true, group);
                });

                var d = new BinaryBlockGroupWindow("Invisible", blocks);
                d.ShowDialog();
                if(d.Result(out var res)) {
                    foreach(var bl in res) {
                        if(bl.group == BinaryBlockGroupWindow.Group.This) {
                            colormap.FilterManager.AddBlockWithoutHearth(colormap.Block.GetId(bl.name), colormap.FilterManager.Invis);
                        } else if(bl.group == BinaryBlockGroupWindow.Group.Def) {
                            ushort id = colormap.Block.GetId(bl.name);
                            if(colormap.FilterManager.Invis.Blocks.Contains(id)) {
                                var hearth = colormap.FilterManager.HearthValue.GetValueOrDefault(id, colormap.FilterManager.Default);
                                colormap.FilterManager.AddBlockWithoutHearth(colormap.Block.GetId(bl.name), hearth);
                            }
                        }
                    }
                }
            };


            upd_meth_link.Click += (o, e) => {
                // depr
            };
            btn_change.Click += (o, e) => {
                Global.App.SettingsHub.SetFromBack();
            };
            btn_undo.Click += (o, e) => {
                Global.App.SettingsHub.Reset();
            };
            btn_colormapfolder.Click += (_, _) => {
                try {
                    Process.Start("explorer.exe", Path.Combine(Global.App.APPFOLDER, "colormaps"));
                }
                catch { }
            };

            light_blue_b = this.TryFindResource("LIGHT_BLUE_B") as Brush;

            tabs = new[] { tab_general, tab_shades, tab_color };
            contents = new[] { cont_general, cont_shades, cont_color };

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


                generalfilter.MaxHeight = generalfilter.ActualHeight;
                generalfilter.ItemsSource = Global.App.Colormap?.FilterManager.Invis.Blocks.Where(bl => bl != Colormap.NONEBLOCK && bl != Colormap.INVBLOCK).Select(bl => new BinaryBlockRow(Global.App.Colormap.Block.GetName(bl), true, false));

                btn_undo.Margin = new Thickness(btn_undo.Margin.Left + btn_change.ActualWidth + btn_undo.ActualWidth + 20, btn_undo.Margin.Top, btn_undo.Margin.Right, btn_undo.Margin.Bottom);
            };
        }
        public void RefreshGeneralFilter() {
            if(loaded) generalfilter.ItemsSource = Global.App.Colormap?.FilterManager.Invis.Blocks.Where(bl => bl != Colormap.NONEBLOCK && bl != Colormap.INVBLOCK).Select(bl => new BinaryBlockRow(Global.App.Colormap.Block.GetName(bl), true, false));
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
            {
                BindingOperations.ClearBinding(generalfilter_header_star, TextBlock.ForegroundProperty);
                generalfilter.ItemsSource = null;
                generalfilter_header_star.Visibility = colormap != null ? Visibility.Visible : Visibility.Hidden;
                if(colormap != null) {
                    MultiBinding foregr = new MultiBinding() { Converter = new DifferenceConverter((a, b) => (a as HashSet<ushort>).SequenceEqual(b as HashSet<ushort>)) };
                    foregr.Bindings.Add(new Binding("BLOCKS") { Source = colormap.FilterManager.Invis });
                    foregr.Bindings.Add(new Binding("Blocks") { Source = colormap.FilterManager.Invis });
                    generalfilter_header_star.SetBinding(TextBlock.ForegroundProperty, foregr);

                    RefreshGeneralFilter();
                }

                colormap.FilterManager.Invis.PropertyChanged += (o, e) => {
                    if(e.PropertyName == nameof(GroupElement<Filter>.Blocks)) {
                        RefreshGeneralFilter();
                    }
                };
            }

            tintGrid.RowDefinitions.Clear();
            tintGrid.Children.Clear();
            filterGrid.RowDefinitions.Clear();
            filterGrid.Children.Clear();

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

                    addtint(i, colormap, tintGrid, tint);

                    i++;
                }
                if(i > 0) tintGrid.RowDefinitions.RemoveAt(tintGrid.RowDefinitions.Count - 1);
                tintSettings.Visibility = i > 0 ? Visibility.Visible : Visibility.Collapsed;



                i = 0;
                foreach(var filter in colormap.FilterManager.ELEMENTS) {
                    if(filter.visible == false) continue;
                    if(Global.Settings.ENABLE_COLORMAP_EDITING == false && filter.caneditsettings == false) {
                        continue;
                    }

                    if(i % 2 == 0) {
                        filterGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                        filterGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(2) });
                        filterGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto, MinHeight = 10 });
                        filterGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(25) });
                    }

                    if(filter == colormap.FilterManager.Depth) adddepthfilter(i);
                    else addfilter(i, colormap, filterGrid, filter);

                    i++;
                }
                if(i > 0) filterGrid.RowDefinitions.RemoveAt(filterGrid.RowDefinitions.Count - 1);
                filterGrid.Visibility = i > 1 ? Visibility.Visible : Visibility.Collapsed;
                filterSettings.Visibility = i > 1 ? Visibility.Visible : Visibility.Collapsed;
            }

        }

        void addtint(int i, Colormap colormap, Grid tintGrid, Tint tint) {
            if(tint is DynamicTint dtint) {
                var dockPanel = new DockPanel();
                Grid.SetRow(dockPanel, (i / 2) * 4);
                Grid.SetColumn(dockPanel, (i % 2) * 2);

                FrameworkElement txtname;
                TextBlock txtnametext;
                if(Global.Settings.ENABLE_COLORMAP_EDITING) {
                    txtname = new LinkTextBlock(false);
                    txtnametext = ((LinkTextBlock)txtname).TextBlock;

                    ((LinkTextBlock)txtname).Click += (_, _) => {
                        var blocks = colormap.Block.All().Select(n => {
                            Tint blocktint = colormap.TintManager.Elements.FirstOrDefault(t => t.Blocks.Contains(n.Key));/* = colormap.TintManager.GetBlockVal(n.Key);*/
                            BinaryBlockGroupWindow.Group group = BinaryBlockGroupWindow.Group.Def;
                            if(blocktint == tint) group = BinaryBlockGroupWindow.Group.This;
                            else if(blocktint != null && blocktint != colormap.TintManager.Default) group = BinaryBlockGroupWindow.Group.Other;

                            return (n.Value, true, group);
                        });
                        var d = new BinaryBlockGroupWindow(tint.name, blocks);
                        d.ShowDialog();
                        if(d.Result(out var res)) {
                            foreach(var bl in res) {
                                ushort id = colormap.Block.GetId(bl.name);

                                if(tint.Blocks.Contains(id)) {
                                    if(bl.group == BinaryBlockGroupWindow.Group.Def) colormap.TintManager.AddBlock(colormap.Block.GetId(bl.name), null);
                                } else {
                                    if(bl.group == BinaryBlockGroupWindow.Group.This) colormap.TintManager.AddBlock(colormap.Block.GetId(bl.name), tint);
                                }
                            }
                        }
                    };

                    var txtstar = new TextBlock() { };
                    txtstar.Inlines.Add(GlobalXaml.ChangeStar("BLOCKS", "Blocks", tint, (a, b) => (a as HashSet<ushort>).SequenceEqual(b as HashSet<ushort>)));

                    dockPanel.Children.Add(txtname);
                    dockPanel.Children.Add(txtstar);
                } else {
                    txtname = new TextBlock();
                    txtnametext = (TextBlock)txtname;

                    dockPanel.Children.Add(txtname);
                }
                var tintmeta = TintMeta.GetFormat(tint.GetType());
                txtnametext.Inlines.Add(new Run() { Text = dtint.name + "/" });
                txtnametext.Inlines.Add(new Run() { Text = tintmeta.kurzformat, FontStyle = FontStyles.Italic, FontSize = 12, Foreground = light_blue_b });
                txtnametext.Inlines.Add(new Run() { Text = "/" });

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
        }

        void addfilter(int i, Colormap colormap, Grid filterGrid, Filter filter) {
            var dockPanel = new DockPanel();
            Grid.SetRow(dockPanel, (i / 2) * 4);
            Grid.SetColumn(dockPanel, (i % 2) * 2);

            FrameworkElement txtname;
            TextBlock txtnametext;
            if(Global.Settings.ENABLE_COLORMAP_EDITING) {
                txtname = new LinkTextBlock(false);
                txtnametext = ((LinkTextBlock)txtname).TextBlock;

                ((LinkTextBlock)txtname).Click += (_, _) => {
                    var blocks = colormap.Block.All().Select(n => {
                        Filter blockfilter = colormap.FilterManager.Elements.FirstOrDefault(t => t.Blocks.Contains(n.Key));/* = colormap.TintManager.GetBlockVal(n.Key);*/
                        BinaryBlockGroupWindow.Group group = BinaryBlockGroupWindow.Group.Def;
                        if(blockfilter == filter) group = BinaryBlockGroupWindow.Group.This;
                        else if(blockfilter != null && blockfilter != colormap.FilterManager.Default) group = BinaryBlockGroupWindow.Group.Other;

                        return (n.Value, true, group);
                    });
                    var d = new BinaryBlockGroupWindow(filter.name, blocks);
                    d.ShowDialog();
                    if(d.Result(out var res)) {
                        foreach(var bl in res) {
                            ushort id = colormap.Block.GetId(bl.name);

                            if(filter.Blocks.Contains(id)) {
                                if(bl.group == BinaryBlockGroupWindow.Group.Def) colormap.FilterManager.AddBlock(colormap.Block.GetId(bl.name), null);
                            } else {
                                if(bl.group == BinaryBlockGroupWindow.Group.This) colormap.FilterManager.AddBlock(colormap.Block.GetId(bl.name), filter);
                            }
                        }
                    }
                };
            } else {
                txtname = txtnametext = new TextBlock();
            }

            var starr = new TextBlock() { Text = " ✶" };          
            {
                FrameworkElement el_absorb = new FrameworkElement();
                MultiBinding bind_absorb = new MultiBinding() { Converter = new DifferenceBoolConverter(), ConverterParameter = true };
                bind_absorb.Bindings.Add(new Binding("ABSORBTION") { Source = filter });
                bind_absorb.Bindings.Add(new Binding("Absorbtion") { Source = filter });
                el_absorb.SetBinding(FrameworkElement.TagProperty, bind_absorb);
                dockPanel.Children.Add(el_absorb);

                FrameworkElement el_elements = new FrameworkElement();
                MultiBinding bind_elements = new MultiBinding() { Converter = new DifferenceBoolConverter((a, b) => (a as HashSet<ushort>).SequenceEqual(b as HashSet<ushort>)), ConverterParameter = true };
                bind_elements.Bindings.Add(new Binding("BLOCKS") { Source = filter });
                bind_elements.Bindings.Add(new Binding("Blocks") { Source = filter });
                el_elements.SetBinding(FrameworkElement.TagProperty, bind_elements);
                dockPanel.Children.Add(el_elements);

                FrameworkElement el_combined = new FrameworkElement();
                MultiBinding bind_combined = new MultiBinding() { Converter = new BitAndConverter() };
                bind_combined.Bindings.Add(new Binding("Tag") { Source = el_absorb });
                bind_combined.Bindings.Add(new Binding("Tag") { Source = el_elements });
                el_combined.SetBinding(FrameworkElement.TagProperty, bind_combined);
                dockPanel.Children.Add(el_combined);

                MultiBinding bind_foreground = new MultiBinding() { Converter = new DifferenceConverter() };
                bind_foreground.Bindings.Add(new Binding("Tag") { Source = el_combined });
                starr.SetBinding(Run.ForegroundProperty, bind_foreground);
            }

            txtnametext.Inlines.Add(new Run() { Text = filter.name, FontStyle = (filter == colormap.FilterManager.Invis || filter == colormap.FilterManager.Default || filter == colormap.FilterManager.Depth) ? FontStyles.Italic : FontStyles.Normal });

            dockPanel.Children.Add(txtname);
            dockPanel.Children.Add(starr);


            var txtTransp = new TextBlock() { HorizontalAlignment = HorizontalAlignment.Right };
            Binding transpBind = new Binding("Absorbtion") { Source = filter, Converter = new PercentNumberReverseConverter(), ConverterParameter = 15 };
            txtTransp.SetBinding(TextBlock.TextProperty, transpBind);
            dockPanel.Children.Add(txtTransp);

            filterGrid.Children.Add(dockPanel);

            var slider = new Slider() {
                IsSnapToTickEnabled = true,
                Minimum = 0,
                Maximum = 15,
                TickFrequency = 1
            };
            Grid.SetRow(slider, (i / 2) * 4 + 2);
            Grid.SetColumn(slider, (i % 2) * 2);
            slider.SetBinding(Slider.ValueProperty, new Binding("Absorbtion") { Source = filter, Converter = new ReverseConverter(), ConverterParameter = 15 });
            //slider.SetBinding(Slider.IsEnabledProperty, new Binding("caneditsettings") { Source = filter });

            filterGrid.Children.Add(slider);

            var isenabledbinding = new Binding("TransparentLayers") { Source = Global.Settings, Converter = new GreaterThanConverter(), ConverterParameter = 1 };
            var isenabledbindingslider = new MultiBinding() { Converter = new BitAndConverter() };
            isenabledbindingslider.Bindings.Add(isenabledbinding);
            isenabledbindingslider.Bindings.Add(new Binding("caneditsettings") { Source = filter });
            slider.SetBinding(Slider.IsEnabledProperty, isenabledbindingslider);
            txtTransp.SetBinding(TextBlock.IsEnabledProperty, isenabledbindingslider);

            var isenabledbindingdock = new MultiBinding() { Converter = new BitOrConverter() };
            isenabledbindingdock.Bindings.Add(isenabledbinding);
            isenabledbindingdock.Bindings.Add(new Binding("ABSORBTION") { Source = filter, Converter = new LessThanConverter(), ConverterParameter = 0.001 });
            dockPanel.SetBinding(Slider.IsEnabledProperty, isenabledbindingdock);
        }

        void adddepthfilter(int i) {
            var dockPanel = new DockPanel();
            Grid.SetRow(dockPanel, (i / 2) * 4);
            Grid.SetColumn(dockPanel, (i % 2) * 2);

            var txtname = new TextBlock() { Text = "depth", FontStyle = FontStyles.Italic };
            dockPanel.Children.Add(txtname);

            var txtTransp = new TextBlock() { HorizontalAlignment = HorizontalAlignment.Right };
            Binding transpBind = new Binding("WaterTransparency") { Source = Global.Settings, Converter = new RoundConverter(), ConverterParameter = 2 };
            txtTransp.SetBinding(TextBlock.TextProperty, transpBind);
            dockPanel.Children.Add(txtTransp);

            filterGrid.Children.Add(dockPanel);

            var slider = new Slider() {
                IsSnapToTickEnabled = false,
                Minimum = 0,
                Maximum = 1,
                TickFrequency = 0.0666
            };
            Grid.SetRow(slider, (i / 2) * 4 + 2);
            Grid.SetColumn(slider, (i % 2) * 2);
            slider.SetBinding(Slider.ValueProperty, new Binding("WaterTransparency") { Source = Global.Settings });

            filterGrid.Children.Add(slider);

            var isenabledbinding = new Binding("TransparentLayers") { Source = Global.Settings, Converter = new GreaterThanConverter(), ConverterParameter = 0 };
            slider.SetBinding(Slider.IsEnabledProperty, isenabledbinding);
            dockPanel.SetBinding(Slider.IsEnabledProperty, isenabledbinding);
        }
    }
}
