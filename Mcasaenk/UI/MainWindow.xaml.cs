using Mcasaenk.Resources;
using Mcasaenk.UI.Canvas;
using Microsoft.Windows.Themes;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
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

namespace Mcasaenk.UI {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public FooterInterface footer;

        LeftSettingsMenu leftSettingsMenu;
        LeftFileMenu leftFileMenu;
        LeftOptionsMenu leftOptionsMenu;
        public MainWindow() {
            InitializeComponent();

            footer = footerControl.@interface;
            footerControl.Init();

            this.resolution_reset.Click += (o, e) => {
                rad.Reset();
            };


            overworld_fore = overworld_back;
            overworld_fore.A = 160;
            nether_fore = nether_back;
            nether_fore.A = 220;
            end_fore = end_back;
            end_fore.A = 120;
            {
               void onchange() {
                    if(Global.App.OpenedSave == null || Global.App.OpenedSave is DimensionSave) {
                        dim_setNone();
                        return;
                    }
                    switch(Global.Settings.DIMENSION) {
                        case Dimension.Type.Overworld:
                            dim_setOverworld(); break;
                        case Dimension.Type.Nether:
                            dim_setNether(); break;
                        case Dimension.Type.End:
                            dim_setEnd(); break;
                    }
                }
                Global.Settings.PropertyChanged += (object sender, PropertyChangedEventArgs e) => { 
                    if(e.PropertyName == nameof(Settings.DIMENSION)) onchange(); 
                };
                btn_overworld.Click += (o, e) => {
                    Global.Settings.DIMENSION = Dimension.Type.Overworld;
                };
                btn_nether.Click += (o, e) => {
                    Global.Settings.DIMENSION = Dimension.Type.Nether;
                };
                btn_end.Click += (o, e) => {
                    Global.Settings.DIMENSION = Dimension.Type.End;
                };
            }

            this.Loaded += (o, e) => {
                var res = Resolution.CurrentResolution(this);
                Resolution.predefined[0].X = res.w;
                Resolution.predefined[0].Y = res.h;

                Resolution.custom.X++;


                this.MinWidth = mainGrid.ColumnDefinitions[0].ActualWidth + 10 + 15;


                leftSettingsMenu = new LeftSettingsMenu();
                leftFileMenu = new LeftFileMenu(opener_worlds);
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
                            leftFileMenu.OnActive();
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
        Color overworld_fore, nether_fore, end_fore;
        void dim_setOverworld() {
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0.5);
            brush.EndPoint = new Point(1, 0.5);

            brush.GradientStops.Add(new GradientStop(overworld_fore, 0));
            brush.GradientStops.Add(new GradientStop(overworld_fore, 0.37));
            brush.GradientStops.Add(new GradientStop(nether_back, 0.5));
            //brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#bb723232"), 0.62));
            brush.GradientStops.Add(new GradientStop(end_back, 1));

            dim_bor.Background = brush;
        }
        void dim_setNether() {
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0.5);
            brush.EndPoint = new Point(1, 0.5);

            brush.GradientStops.Add(new GradientStop(overworld_back, 0));
            brush.GradientStops.Add(new GradientStop(nether_fore, 0.37));
            brush.GradientStops.Add(new GradientStop(nether_fore, 0.5));
            brush.GradientStops.Add(new GradientStop(nether_fore, 0.62));
            brush.GradientStops.Add(new GradientStop(end_back, 1));

            dim_bor.Background = brush;
        }
        void dim_setEnd() {
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0.5);
            brush.EndPoint = new Point(1, 0.5);

            brush.GradientStops.Add(new GradientStop(overworld_back, 0));
            // brush.GradientStops.Add(new GradientStop(overworld_fore, 0.37));
            brush.GradientStops.Add(new GradientStop(nether_back, 0.5));
            brush.GradientStops.Add(new GradientStop(end_fore, 0.62));
            brush.GradientStops.Add(new GradientStop(end_fore, 1));

            dim_bor.Background = brush;
        }
        void dim_setNone() {
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0.5);
            brush.EndPoint = new Point(1, 0.5);

            brush.GradientStops.Add(new GradientStop(overworld_back, 0));
            brush.GradientStops.Add(new GradientStop(nether_back, 0.5));
            brush.GradientStops.Add(new GradientStop(end_back, 1));

            dim_bor.Background = brush;
        }





        public void OnHardReset() {
            leftSettingsMenu.SetUpTintGrid(Global.App.Colormap);

            btn_overworld.IsEnabled = Global.App.OpenedSave?.overworld != null;
            btn_nether.IsEnabled = Global.App.OpenedSave?.nether != null;
            btn_end.IsEnabled = Global.App.OpenedSave?.end != null;

            currs.Content = Global.App.OpenedSave?.levelDat;
            wrldPanel.Visibility = Global.App.OpenedSave?.levelDat != null ? Visibility.Visible : Visibility.Collapsed;
            //Grid.SetRow(screenshotPanel, Global.App.OpenedSave?.levelDat != null ? 2 : 0);

            if(Global.App.OpenedSave is DimensionSave) {
                dim_setNone();
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
}