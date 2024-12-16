using CommunityToolkit.HighPerformance;
using Mcasaenk.Resources;
using Mcasaenk.UI.Canvas;
using Mcasaenk.WorldInfo;
using Microsoft.Windows.Themes;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.Printing;
using System.Resources;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using static System.Formats.Asn1.AsnWriter;

namespace Mcasaenk.UI {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public FooterInterface footer;

        LeftSettingsMenu leftSettingsMenu;
        LeftFileMenu leftFileMenu;
        LeftOptionsMenu leftOptionsMenu;

        ResolutionScale resScale = new ResolutionScale();

        public MainWindow() {
            InitializeComponent();

            this.Title = "MCA Saenk v" + App.VERSION;

            footer = footerControl.@interface;
            footerControl.Init();

            this.scr_capture.Click += (o, e) => {
                var screenshottaker = canvasControl?.ScreenshotManager;
                if(screenshottaker == null) return;
                if(screenshottaker.Rect().Width == 0 || screenshottaker.Rect().Height == 0) {
                    MessageBox.Show("Cannot make screenshot with no width/height :(");
                    return;
                }
                if(screenshottaker.Rect().Width > 16384 || screenshottaker.Rect().Height > 16384) {
                    MessageBox.Show("The size of the screenshot is too large\nThe maximum in both width and height is 16384");
                    return;
                }
                canvasControl?.ScreenshotManager?.TakeAndSaveScreenshot();
            };
            this.scr_stop.Click += (o, e) => {
                rad.Reset(true);
            };
            this.scr_rotate.Click += (o, e) => {
                canvasControl?.ScreenshotManager.Rotate();
            };
            this.rad.SetCallback(() => {
                var res = this.rad.GetResolution();
                if(res?.type == ResolutionType.frame) {
                    var canvasSize = canvasControl.ScreenSize();
                    res.X = (int)Math.Ceiling(canvasSize.Width) + 1;
                    res.Y = (int)Math.Ceiling(canvasSize.Height) + 1;
                }
                if(res?.type == ResolutionType.map) {
                    scale.Items.Clear();
                    scale.Items.Add("1:1");
                    scale.Items.Add("1:2");
                    scale.Items.Add("1:4");
                    scale.Items.Add("1:8");
                    scale.SelectedIndex = 0;
                } else {
                    scale.Items.Clear();
                    scale.Items.Add("1:1");
                    scale.Items.Add("2:1");
                    scale.Items.Add("4:1");
                    scale.SelectedIndex = 0;
                }
                if(res?.type == ResolutionType.resizeable) {
                    scale.SelectedIndex = 0;
                    scale.IsEnabled = false;
                } else {
                    scale.IsEnabled = true;
                }

                canvasControl?.SetUpScreenShot(res, resScale, res?.type == ResolutionType.resizeable);

                scr_capture.IsEnabled = res != null;
                scr_stop.IsEnabled = res != null;
                scr_rotate.IsEnabled = res != null;

                if(res?.type == ResolutionType.frame) {
                    scr_capture.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    scr_stop.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }
            });
            rad.ShowSlot3(Global.Settings.USEMAPPALETTE);

            scale.SetBinding(ComboBox.SelectedItemProperty, new Binding("Scale") { Source = resScale, Converter = new ResolutionScaleTextToDouble() });
            scale.SelectedIndex = 0;
            this.resScale.PropertyChanged += (o, e) => {
                if(e.PropertyName == "Scale") {
                    canvasControl.ScreenshotManager?.Rescale();
                }
            };

            {
                Global.Settings.PropertyChanged += (object sender, PropertyChangedEventArgs e) => {
                    if(e.PropertyName == nameof(Settings.DIMENSION)) dim_onchange();
                };
                btn_dim_overworld.Click += (o, e) => {
                    Global.Settings.DIMENSION = "minecraft:overworld";
                };
                btn_dim_nether.Click += (o, e) => {
                    Global.Settings.DIMENSION = "minecraft:the_nether";
                };
                btn_dim_end.Click += (o, e) => {
                    Global.Settings.DIMENSION = "minecraft:the_end";
                };
                btn_dim_others.Click += (o, e) => {
                    string current = Global.Settings.DIMENSION;
                    var currColor = (Color)ColorConverter.ConvertFromString("#70a0b2");
                    (TextBlock text, object data) createdim(string dim) {
                        TextBlock text = new TextBlock();
                        text.Inlines.Add(new Run() { Text = dim.Split(':')[0] + ':', FontSize = 12 });
                        text.Inlines.Add(new Run() { Text = dim.Split(':')[1], FontSize = 14 });
                        if(dim == current) {
                            text.Foreground = new SolidColorBrush(currColor);
                            text.FontWeight = FontWeights.DemiBold;
                        }
                        return (text, dim);
                    }

                    var w = new ListOptionDialog("Other dimensions",
                        Global.App.OpenedSave.dimensions.Select(d => d.name).Where(n => !n.StartsWith("minecraft:")).Select(d => createdim(d)).ToArray());

                    w.ShowDialog();
                    if(w.Result(out object _res)) {
                        string res = (string)_res;
                        Global.Settings.DIMENSION = res;
                        (btn_dim_others.Content as Image).Source = Global.App.OpenedSave.GetDimension(Global.Settings.DIMENSION).image;
                    }
                };
            }

            this.Loaded += (o, e) => {
                rad.PreDefined(Global.Settings.PREDEFINEDRES);

                this.MinWidth = mainGrid.ColumnDefinitions[0].ActualWidth + 10 + 15;


                leftSettingsMenu = new LeftSettingsMenu();
                leftFileMenu = new LeftFileMenu(opener_worlds);
                leftFileMenu.OnActive();
                leftOptionsMenu = new LeftOptionsMenu();

                var leftsl = new PageSlider(this, true, true, () => (int)mainGrid.ColumnDefinitions[0].ActualWidth - (int)opener_worlds.ActualWidth - 10, 10,
                    ss, settings_cont, [
                        (opener_sett, () => {
                            settings_cont.Child = leftSettingsMenu;
                            leftSettingsMenu.OnActive();
                        }
                        ),
                        (opener_post, () => {
                            settings_cont.Child = leftOptionsMenu;
                            leftOptionsMenu.OnActive();
                        }
                        ),
                        (opener_worlds, () => {
                            settings_cont.Child = leftFileMenu;
                            mainGrid.UpdateLayout();
                            //leftFileMenu.OnActive();
                        }
                        )]);

                //RightSettingsMenu rightSettingsMenu = new RightSettingsMenu();
                //var righttsl = new Slider(this, false, false, () => (int)grgr.ColumnDefinitions[1].ActualWidth - (int)render_sett.ActualWidth - 20, 0,
                //    tt, settings_cont2, [
                //        (render_sett, () => {
                //            settings_cont2.Child = rightSettingsMenu;
                //        }), 
                //        (post_sett, () => {
                //            settings_cont2.Child = null;
                //        }
                //        )]);

                currs_icon_btn.MouseEnter += (sender, e) => {
                    currs_icon.Source = WPFBitmap.FromBytes(ResourceMapping.folder).ToBitmapSource();
                };
                currs_icon_btn.MouseLeave += (sender, e) => {
                    currs_icon.Source = Global.App.OpenedSave?.levelDatInfo.image;
                };
                currs_icon_btn.Click += (sender, e) => {
                    if(leftsl.GetActive() != opener_worlds) {
                        opener_worlds.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    }
                };

                Global.App.OpenedSave = null;

                opener_worlds.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            };

            {
                loc_go.Click += (o, e) => {
                    if(!int.TryParse(loc_x.Text, out int x)) return;
                    if(!int.TryParse(loc_z.Text, out int z)) return;
                    canvasControl.GoTo(new Point(x, z));
                };

                Brush transp = new SolidColorBrush(Colors.Transparent), fore = (Brush)this.TryFindResource("FORE"), fore_hover = (Brush)this.TryFindResource("FORE_HOVER"), fore_press = (Brush)this.TryFindResource("FORE_PRESS");

                loc_txt.MouseEnter += (o, e) => {
                    loc_border.BorderBrush = fore;
                    loc_txt.Foreground = fore_hover;
                };
                loc_txt.MouseLeave += (o, e) => {
                    loc_border.BorderBrush = transp;
                    loc_txt.Foreground = fore;
                };
                loc_txt.MouseLeftButtonDown += (o, e) => {
                    loc_txt.Foreground = fore_press;
                };
                loc_txt.PreviewMouseLeftButtonUp += (o, e) => {
                    loc_txt.Foreground = fore_hover;
                    List<(TextBlock txtblocks, object data)> options = new();

                    if(Global.App.OpenedSave != null) {
                        LevelDatInfo lvlinfo = Global.App.OpenedSave.levelDatInfo;
                        (TextBlock txtblock, object data) loc(string text, bool enabled, int x, int z) {
                            TextBlock txtblock = new TextBlock();
                            txtblock.IsEnabled = enabled;
                            txtblock.Inlines.AddRange(new Run[] {
                            new Run() { Text = $"{text} (" },
                            new Run() { Text = x.ToString(), Foreground = (Brush)this.TryFindResource("LIGHT_BLUE_B") },
                            new Run() { Text = $", " },
                            new Run() { Text = z.ToString(), Foreground = (Brush)this.TryFindResource("LIGHT_BLUE_B") },
                            new Run() { Text = $")" }
                        });
                            return (txtblock, (x, z));
                        }
                        options.Add(loc("World spawn", lvlinfo.mainPlayer.sp_d == Global.Settings.DIMENSION, lvlinfo.mainPlayer.sp_x, lvlinfo.mainPlayer.sp_z));
                        options.Add(loc("Player1 position", lvlinfo.mainPlayer.pl_d == Global.Settings.DIMENSION, lvlinfo.mainPlayer.pl_x, lvlinfo.mainPlayer.pl_z));

                        int i = 1;
                        foreach(var player in lvlinfo.otherPlayers) {
                            if(i > 1) options.Add(loc($"Player{i} position", player.pl_d == Global.Settings.DIMENSION, player.pl_x, player.pl_z));
                            options.Add(loc($"Player{i} spawn", player.sp_d == Global.Settings.DIMENSION, player.sp_x, player.sp_z));

                            i++;
                        }
                    }

                    var od = new ListOptionDialog("Locations of interest", options.ToArray());
                    od.ShowDialog();
                    if(od.Result(out object _coords)) {
                        var coords = ((int x, int z))_coords;

                        loc_x.Text = coords.x.ToString();
                        loc_z.Text = coords.z.ToString();
                    }
                };
            }
        }

        Color overworld_back = (Color)ColorConverter.ConvertFromString("#664d7132");
        Color nether_back = (Color)ColorConverter.ConvertFromString("#66723232");
        Color end_back = (Color)ColorConverter.ConvertFromString("#66ABB270");
        Color others_back = (Color)ColorConverter.ConvertFromString("#6670a0b2");

        void dim_onchange() {
            if(Global.App.OpenedSave == null) {
                dim_bor.Background = new SolidColorBrush(Colors.Transparent);
                return;
            }

            btn_dim_overworld.Opacity = 0.65;
            btn_dim_nether.Opacity = 0.65;
            btn_dim_end.Opacity = 0.65;
            btn_dim_others.Opacity = 0.65;

            switch(Global.Settings.DIMENSION) {
                case "minecraft:overworld":
                    dim_bor.Background = new SolidColorBrush(overworld_back);
                    btn_dim_overworld.Opacity = 1;
                    break;
                case "minecraft:the_nether":
                    dim_bor.Background = new SolidColorBrush(nether_back);
                    btn_dim_nether.Opacity = 1;
                    break;
                case "minecraft:the_end":
                    dim_bor.Background = new SolidColorBrush(end_back);
                    btn_dim_end.Opacity = 1;
                    break;
                default:
                    dim_bor.Background = new SolidColorBrush(others_back);
                    btn_dim_others.Opacity = 1;
                    break;
            }
        }


        public void OnColormapChange() {
            leftSettingsMenu.SetUpColormapSettings(Global.App.Colormap);
        }


        public void OnHardReset() {
            if(Global.App.OpenedSave != null) {
                btn_dim_overworld.IsEnabled = Global.App.OpenedSave.overworld != null;
                btn_dim_nether.IsEnabled = Global.App.OpenedSave.nether != null;
                btn_dim_end.IsEnabled = Global.App.OpenedSave.end != null;
                btn_dim_others.Visibility = Global.App.OpenedSave.dimensions.Any(d => !d.name.StartsWith("minecraft:")) ? Visibility.Visible : Visibility.Collapsed;
            }

            SetCurrs(Global.App.OpenedSave?.levelDatInfo);
            wrldPanel.Visibility = Global.App.OpenedSave?.levelDatInfo != null ? Visibility.Visible : Visibility.Collapsed;
            title.Visibility = wrldPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

            rad.Reset(Global.App.OpenedSave != null);

            canvasControl.Focus();

            dim_onchange();
        }


        public void SetCurrs(LevelDatInfo level) {
            if(level == null) return;

            currs_icon.Source = level.image;

            currs_lastopened.Text = level.lastopened.ToString();
            currs_folder.Text = level.foldername;
            currs_name.Text = level.name;
            currs_version.Text = level.version_name;
        }

    }

    // noting here
    class PageSlider {
        EButton opener_active;
        public PageSlider(Window window, bool lefttoright, bool fixedw, Func<int> getW, int wM, FrameworkElement ss, FrameworkElement container, List<(EButton btn, Action onopen)> openers) {
            int w = getW();
            container.Width = w - wM;
            //container.Margin = new Thickness(0, 0, wM, 0);

            if(lefttoright) container.Margin = new Thickness(wM, 0, 0, 0);
            else container.Margin = new Thickness(0, 0, wM, 0);

            if(lefttoright) w = -w;

            ss.RenderTransform = new TranslateTransform(w, 0);

            Storyboard left_story = null, right_story = null;

            opener_active = null;
            var activeBorder = window.TryFindResource("BLUE_B") as SolidColorBrush;
            var passiveBorder = window.TryFindResource("BORDER") as SolidColorBrush;

            foreach(var open in openers) {
                var btn = open.btn;
                var onopen = open.onopen;
                btn.Click += (o, e) => {
                    updateAnimatiom();
                    if(handleOpen(btn) == false) return;
                    btn.BorderColor = activeBorder;
                    onopen();
                };
            }
            if(fixedw == false) {
                ss.SizeChanged += (o, e) => {
                    updateAnimatiom();
                    if(opener_active == null) {
                        int w = getW();
                        if(lefttoright) w = -w;
                        ss.RenderTransform = new TranslateTransform(w, 0);
                    }
                };
            }



            void updateAnimatiom() {
                int w = getW();

                container.Width = w - wM;

                //ss.RenderTransform = new TranslateTransform(w - opener_worlds.ActualWidth - 10, 0);
                if(lefttoright) w = -w;
                var right_animation = new DoubleAnimation {
                    From = w,
                    To = 0,
                    Duration = TimeSpan.FromSeconds(0.25),
                };
                var left_animation = new DoubleAnimation {
                    From = 0,
                    To = w,
                    Duration = TimeSpan.FromSeconds(0.25),
                };
                right_story = new Storyboard();
                right_story.Children.Add(right_animation);
                Storyboard.SetTargetProperty(right_animation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

                left_story = new Storyboard();
                left_story.Children.Add(left_animation);
                Storyboard.SetTargetProperty(left_animation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            }

            bool handleOpen(EButton cnt) {
                if(opener_active != null) opener_active.BorderColor = passiveBorder;

                if(opener_active == cnt) {
                    opener_active = null;
                    ss.BeginStoryboard(left_story);
                    return false;
                } else if(opener_active == null) {
                    opener_active = cnt;
                    ss.BeginStoryboard(right_story);
                    return true;
                } else {
                    opener_active = cnt;
                    return true;
                }
            }
        }


        public EButton GetActive() {
            return opener_active;
        }

    }



    public static class GlobalXaml {

        public static void SortByColumn(this DataGrid grid, string columnName, ListSortDirection direction) {
            // Ensure the DataGrid has an ItemsSource
            if(grid.ItemsSource == null)
                return;

            ICollectionView collectionView = CollectionViewSource.GetDefaultView(grid.ItemsSource);

            if(collectionView != null) {
                collectionView.SortDescriptions.Clear();
                collectionView.SortDescriptions.Add(new SortDescription(columnName, direction));
                collectionView.Refresh();
            }
        }

        public static Run ChangeStar(string path1, string path2, object settings, string ch = " ✶") {
            Run r = new Run();
            r.Text = ch;
            MultiBinding foregr = new MultiBinding() { Converter = new DifferenceConverter() };
            foregr.Bindings.Add(new Binding(path1) { Source = settings });
            foregr.Bindings.Add(new Binding(path2) { Source = settings });
            r.SetBinding(Run.ForegroundProperty, foregr);
            return r;
        }

    }
}