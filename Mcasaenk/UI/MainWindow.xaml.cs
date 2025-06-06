﻿using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Mcasaenk.Nbt;
using Mcasaenk.Rendering_bitmap;
using Mcasaenk.Rendering_Opengl;
using Mcasaenk.Resources;
using Mcasaenk.UI.Canvas;
using Mcasaenk.WorldInfo;
using Microsoft.Win32;
using OpenTK.Wpf;

namespace Mcasaenk.UI {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public LeftSettingsMenu leftSettingsMenu;
        public LeftFileMenu leftFileMenu;
        public LeftOptionsMenu leftOptionsMenu;

        public ScreenshotManager screenshot;

        public CanvasCoordinator canvas;
        private FrameworkElement canvasControl;

        ResolutionScale resScale = new ResolutionScale();
        public MainWindow() {
            InitializeComponent();

            this.Title = "MCA Saenk v" + App.VERSION;

            this.scr_capture.Click += (o, e) => {
                if(screenshot == null) return;

                var res = screenshot.Resolution();
                var state = screenshot.GetState(Global.App.TileMap);

                if(res.X > 16384 || res.Z > 16384) {
                    MessageBox.Show("The size of the screenshot is too large\nThe maximum in both width and height is 16384");
                    return;
                } else if(res.X == 0 || res.Z == 0) {
                    MessageBox.Show("Cannot make screenshot with no width/height :(");
                    return;
                }

                if(state == ScreenshotManager.ConditionalState.invalid) return;

                    ScreenshotTaker screenshottaker = canvas.CreateScreenshotCamera(screenshot);
                if(screenshottaker == null) return;
                try {
                    if(screenshot.ResolutionType() == ResolutionType.map) {
                        if(res.X == 128 || res.Z == 128) {
                            var saveFileDialog = new SaveFileDialog {
                                Filter = "Dat file|*.dat",
                                Title = "Save screenshot",
                                FileName = $"map_"
                            };
                            if(saveFileDialog.ShowDialog() == true) {
                                var nbt = screenshottaker.TakeScreenshotAsMap(Global.App.TileMap.dim, Global.App.OpenedSave.levelDatInfo.version_id, Global.Settings.MAPAPPROXIMATIONALGO);
                                if(nbt == null) return;

                                using(var fs = new FileStream(saveFileDialog.FileName, FileMode.Create)) {
                                    using(var zipStream = new GZipStream(fs, CompressionMode.Compress, false)) {
                                        new NbtWriter(zipStream, nbt, "");
                                    }
                                }

                                nbt.Dispose();
                            }

                        } else {
                            MessageBox.Show("The map screenshot must be 128x128");
                            return;
                        }
                    } else {
                        var saveFileDialog = new SaveFileDialog {
                            Filter = "PNG Image|*.png",
                            Title = "Save screenshot",
                            FileName = $"{Global.App.OpenedSave?.levelDatInfo?.name ?? "screenshot"}{res.X}x{res.Z}"
                        };

                        if(saveFileDialog.ShowDialog() == true) {
                            var encoder = new PngBitmapEncoder();
                            var screenshot = screenshottaker.TakeScreenshotAsImage();
                            if(screenshot == null) return;
                            encoder.Frames.Add(BitmapFrame.Create(screenshot));
                            using(var fileStream = new FileStream(saveFileDialog.FileName, FileMode.Create)) {
                                encoder.Save(fileStream);
                            }
                        }
                    }
                } finally {
                    if(screenshottaker is IDisposable disp) disp.Dispose();
                }
            };
            this.scr_stop.Click += (o, e) => {
                rad.Reset(true);
            };
            this.scr_rotate.Click += (o, e) => {
                screenshot?.Rotate();
            };
            this.resScale.PropertyChanged += (o, e) => {
                if(e.PropertyName == nameof(ResolutionScale.Scale)) {
                    Global.Settings.MAPGRID = screenshot?.ResolutionType() == ResolutionType.map ? (MapGridType)((int)Math.Log2(1 / resScale.Scale) + 1) : MapGridType.None;
                }
            };
            this.rad.SetCallback(() => {
                var res = this.rad.GetResolution();
                if(res?.type == ResolutionType.frame) {
                    var canvasSize = canvas.ScreenSize();
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

                Global.Settings._UseMapPalette = res?.type == ResolutionType.map;
                screenshot?.Dispose();
                screenshot = res != null ? new ScreenshotManager(res, resScale, res?.type == ResolutionType.resizeable, canvas.GetScreen().Mid.Floor().Sub(new Point(res.X, res.Y).Dev(resScale.Scale).Dev(2).Floor())) : null;

                Global.Settings.MAPGRID = screenshot?.ResolutionType() == ResolutionType.map ? (MapGridType)((int)Math.Log2(1 / resScale.Scale) + 1) : MapGridType.None;

                scr_capture.IsEnabled = res != null;
                scr_stop.IsEnabled = res != null;
                scr_rotate.IsEnabled = res != null;

                if(res?.type == ResolutionType.frame) {
                    scr_capture.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    scr_stop.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }
            });
            //rad.ShowSlot3(Global.Settings.USEMAPPALETTE);

            scale.SetBinding(ComboBox.SelectedItemProperty, new Binding("Scale") { Source = resScale, Converter = new ResolutionScaleTextToDouble() });
            scale.SelectedIndex = 0;

            {
                Global.Settings.PropertyChanged += (object sender, PropertyChangedEventArgs e) => {
                    if(e.PropertyName == nameof(Settings.DIMENSION)) DimensionSetup();
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
                while(SetCanvas(Global.Settings.RENDERMODE) == false) {
                    Global.Settings.RENDERMODE = Global.IncrementEnumWithWrap(Global.Settings.RENDERMODE);
                }

                opener_worlds.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            };

            {
                loc_go.Click += (o, e) => {
                    if(!int.TryParse(loc_x.Text, out int x)) return;
                    if(!int.TryParse(loc_z.Text, out int z)) return;
                    canvas.GoTo(new Point(x, z));
                };

                Brush transp = new SolidColorBrush(Colors.Transparent), fore = (Brush)this.TryFindResource("FORE"), fore_hover = (Brush)this.TryFindResource("FORE_HOVER"), fore_press = (Brush)this.TryFindResource("FORE_PRESS");

                loc_txt.TextBlock.Text = "Location";
                loc_txt.TextBlock.FontSize = 18;

                loc_txt.Click += (o, e) => {
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

        public bool SetCanvas(RenderMode renderMode) {
            WorldPosition lastpos = canvas != null ? canvas.GetScreen() : WorldPosition.Empty;
            switch(renderMode) {
                case RenderMode.OPENGL: {
                        var control = new GLWpfControl();
                        if(Shader.StartOpenGL(control)) {
                            this.canvas = new GLCanvasCoordinator(control, lastpos);
                            this.canvasControl = control;
                            break;
                        } else {
                            control.Dispose();
                            return false;
                        }
                    }

                case RenderMode.LEGACY: {
                        var control = new WPFCanvas.OnRenderFrameworkElement();
                        this.canvas = new WPFCanvas(control, lastpos);
                        this.canvasControl = control;
                        break;
                    }
            }

            canvasHolder.Children.Clear();
            canvasHolder.Children.Add(canvasControl);

            return true;
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

            screenshot?.Dispose();
            screenshot = null;

            SetCurrs(Global.App.OpenedSave?.levelDatInfo);
            wrldPanel.Visibility = Global.App.OpenedSave?.levelDatInfo != null ? Visibility.Visible : Visibility.Collapsed;
            title.Visibility = wrldPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

            rad.Reset(Global.App.OpenedSave != null);

            canvasControl.Focus();

            DimensionSetup();

            screenmsg.Visibility = Visibility.Collapsed;
            if(Global.App.TileMap != null) {
                if(Global.App.TileMap.IsEmpty()) {
                    screenmsg.Text = "This dimension is empty";
                    screenmsg.Visibility = Visibility.Visible;
                }
            }
        }


        private void SetCurrs(LevelDatInfo level) {
            if(level == null) return;

            currs_icon.Source = level.image;

            currs_lastopened.Text = level.lastopened.ToString();
            currs_folder.Text = level.foldername;
            currs_name.Text = level.name;
            currs_version.Text = level.version_name;
        }
        private void DimensionSetup() {
            if(Global.App.OpenedSave == null) {
                dim_bor.Background = new SolidColorBrush(Colors.Transparent);
                return;
            }

            btn_dim_overworld.Opacity = 0.65;
            btn_dim_nether.Opacity = 0.65;
            btn_dim_end.Opacity = 0.65;
            btn_dim_others.Opacity = 0.65;

            Color overworld_back = (Color)ColorConverter.ConvertFromString("#664d7132");
            Color nether_back = (Color)ColorConverter.ConvertFromString("#66723232");
            Color end_back = (Color)ColorConverter.ConvertFromString("#66ABB270");
            Color others_back = (Color)ColorConverter.ConvertFromString("#6670a0b2");

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

        public static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject {
            for(int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
                var child = VisualTreeHelper.GetChild(parent, i);
                if(child is T result)
                    return result;

                var childOfChild = FindVisualChild<T>(child);
                if(childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

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

        public static Run ChangeStar(string path1, string path2, object settings, DifferenceConverter.Compare cmp = null, string ch = " ✶") {
            Run r = new Run();
            r.Text = ch;
            MultiBinding foregr = new MultiBinding() { Converter = new DifferenceConverter(cmp) };
            foregr.Bindings.Add(new Binding(path1) { Source = settings });
            foregr.Bindings.Add(new Binding(path2) { Source = settings });
            r.SetBinding(Run.ForegroundProperty, foregr);
            return r;
        }

    }
}