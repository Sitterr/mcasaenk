using CommunityToolkit.HighPerformance;
using Mcasaenk.Resources;
using Mcasaenk.UI.Canvas;
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
                canvasControl?.ScreenshotManager?.TakeAndSaveScreenShot();
            };
            this.scr_stop.Click += (o, e) => {
                rad.Reset(true);
            };
            this.scr_rotate.Click += (o, e) => {
                canvasControl?.ScreenshotManager.Rotate();
            };
            this.rad.SetCallback(() => {
                var res = this.rad.GetResolution();
                if(res.type == Rad.ResolutionType.frame) {
                    var canvasSize = canvasControl.ScreenSize();
                    res.res.X = (int)Math.Ceiling(canvasSize.Width) + 1;
                    res.res.Y = (int)Math.Ceiling(canvasSize.Height) + 1;
                }
                if(res.type == Rad.ResolutionType.resizeable) {
                    scale.SelectedIndex = 0;
                    scale.IsEnabled = false;
                } else {
                    scale.IsEnabled = true;
                }

                canvasControl?.SetUpScreenShot(res.res, resScale, res.type == Rad.ResolutionType.resizeable);

                scr_capture.IsEnabled = res.res != null;
                scr_stop.IsEnabled = res.res != null;
                scr_rotate.IsEnabled = res.res != null;

                if(res.type == Rad.ResolutionType.frame) {
                    scr_capture.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    scr_stop.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }
            });
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
                    var w = new CustomDimensionSelectorWindow(
                        Global.App.OpenedSave.dimensions
                        .Select(d => d.name).Where(n => !n.StartsWith("minecraft:")).ToArray(), 
                        Global.Settings.DIMENSION);

                    w.ShowDialog();
                    string res = w.Result();
                    if(res != "") {
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

                Global.App.OpenedSave = null;

                opener_worlds.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            };

            loc_go.Click += (o, e) => {
                if(!int.TryParse(loc_x.Text, out int x)) return;
                if(!int.TryParse(loc_z.Text, out int z)) return;
                canvasControl.GoTo(new Point(x, z));
            };
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

            currs.Content = Global.App.OpenedSave?.levelDatInfo;
            wrldPanel.Visibility = Global.App.OpenedSave?.levelDatInfo != null ? Visibility.Visible : Visibility.Collapsed;
            //Grid.SetRow(screenshotPanel, Global.App.OpenedSave?.levelDat != null ? 2 : 0);

            rad.Reset(Global.App.OpenedSave != null);

            canvasControl.Focus();

            dim_onchange();
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
}