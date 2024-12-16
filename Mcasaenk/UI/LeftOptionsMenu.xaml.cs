using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// Interaction logic for LeftOptionsMenu.xaml
    /// </summary>
    public partial class LeftOptionsMenu : UserControl {
        EButton[] tabs;
        FrameworkElement[] contents;
        public LeftOptionsMenu() {
            InitializeComponent();

            txt_version.Text = App.VERSION;

            upd_meth_link.Click += (o, e) => {
                // depr
            };
            btn_change.Click += (o, e) => {
                Global.App.SettingsHub.SetFromBack();
            };
            btn_undo.Click += (o, e) => {
                Global.App.SettingsHub.Reset();
            };

            tabs = new[] { tab_config, tab_about };
            contents = new[] { cont_config, cont_about };

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

                tab_config.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));


                btn_undo.Margin = new Thickness(btn_undo.Margin.Left + btn_change.ActualWidth + btn_undo.ActualWidth + 20, btn_undo.Margin.Top, btn_undo.Margin.Right, btn_undo.Margin.Bottom);
            };

            if(Global.Settings.PREDEFINEDRES.Length < 3) {
                var newarr = new Resolution[3];
                for(int i = 0; i < Global.Settings.PREDEFINEDRES.Length; i++) newarr[i] = Global.Settings.PREDEFINEDRES[i];
                for(int j = Global.Settings.PREDEFINEDRES.Length; j < 3; j++) newarr[j] = new Resolution() { Name = "Full HD", type = ResolutionType.stat, X = 1920, Y = 1080 };
                Global.Settings.PREDEFINEDRES = newarr;
            }
            res0.Content = Global.Settings.PREDEFINEDRES[0];
            res1.Content = Global.Settings.PREDEFINEDRES[1];
            res2.Content = Global.Settings.PREDEFINEDRES[2];
        }

        public void OnActive() { 
        
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e) {
            var url = e.Uri.ToString();
            Process.Start(new ProcessStartInfo(url) {
                UseShellExecute = true
            });
        }
    }

}
