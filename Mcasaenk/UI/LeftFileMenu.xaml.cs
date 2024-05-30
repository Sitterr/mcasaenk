using Mcasaenk.Rendering.ChunkRenderData;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace Mcasaenk.UI {
    /// <summary>
    /// Interaction logic for LeftFileMenu.xaml
    /// </summary>
    public partial class LeftFileMenu : UserControl {

        EButton[] tabs;
        FrameworkElement[] contents;
        EButton opener_worlds;
        public LeftFileMenu(EButton opener_worlds) {
            this.opener_worlds = opener_worlds;
            InitializeComponent();

            tabs = new[] { tab_java, tab_folder };
            contents = new[] { cont_java, cont_folder };

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

                tab_java.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            };


            btn_browse.Click += (o, e) => {
                // Configure open file dialog box
                var dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.FileName = "Open"; // Default file name
                dialog.DefaultExt = ".dat"; // Default file extension
                dialog.Filter = "Minecraft level file (.dat)|level.dat"; // Filter files by extension

                // Show open file dialog box
                bool? result = dialog.ShowDialog();

                // Process open file dialog box results
                if(result == true) {
                    // Open document
                    string filename = dialog.FileName;

                    // code here
                    Global.App.OpenedSave = new Save(Path.GetDirectoryName(filename));

                    opener_worlds.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }
            };

            btn_browse_dim.Click += (o, e) => {
                var dialog = new Microsoft.Win32.OpenFolderDialog();
                dialog.Multiselect = false;

                bool? result = dialog.ShowDialog();

                if(result == true) {
                    string path = dialog.FolderName;

                    Global.App.OpenedSave = new DimensionSave(path);

                    opener_worlds.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }
            };


            btn_retry.Click += (o, e) => {
                OnActive();
            };

            btn_fld.Click += (o, e) => {
                var dialog = new Microsoft.Win32.OpenFolderDialog();
                dialog.Multiselect = false;

                bool? result = dialog.ShowDialog();

                if(result == true) {
                    string path = dialog.FolderName;

                    Global.App.Settings.MCDIR = path;
                    OnActive();
                }
            };
        }

        public void OnActive() {
            var saveTemp = (DataTemplate)FindResource("saveTemp");
            javaCont.Children.Clear();

            int i = 0;
            foreach(var _dir in Directory.GetDirectories(Global.Settings.McDir)) {
                var dir = _dir;
                var level = LevelDat.ReadWorld(dir);
                if(level == null) continue;

                var b = new Border();
                b.BorderBrush = (Brush)FindResource("BORDER");
                var f = new ContentControl() { ContentTemplate = saveTemp, Content = level };
                f.Margin = new Thickness(0, 8, 0, 8);
                var btn = new EButton() { Background2 = new SolidColorBrush(Colors.Transparent) };
                btn.BorderThickness = new Thickness(0);
                btn.HorizontalContentAlignment = HorizontalAlignment.Left; btn.VerticalContentAlignment = VerticalAlignment.Top;
                btn.Content = f;

                b.Child = btn;
                b.BorderThickness = new Thickness(0, i == 0 ? 1 : 0, 0, 1);

                btn.Click += (o, e) => {
                    Global.App.OpenedSave = new Save(dir, level);

                    opener_worlds.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                };

                javaCont.Children.Add(b);
                i++;
            }

            emptyCont.Visibility = i == 0 ? Visibility.Visible : Visibility.Collapsed;
            //javaCont.Visibility = i != 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
