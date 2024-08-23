using Mcasaenk.Rendering.ChunkRenderData;
using Mcasaenk.WorldInfo;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
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
        DataTemplate saveTemp;
        public LeftFileMenu(EButton opener_worlds) {
            this.opener_worlds = opener_worlds;
            InitializeComponent();

            saveTemp = (DataTemplate)FindResource("saveTemp");

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
                    Global.App.OpenedSave = Save.FromPath(Path.GetDirectoryName(filename));

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


            orderProperty.Items.Add("Name");
            orderProperty.Items.Add("Version");
            orderProperty.Items.Add("Last open");

            orderMode.Items.Add("Asc");
            orderMode.Items.Add("Desc");


            filterName.TextChanged += (o, e) => { if(!fr) FilterJava(); };
            filterVersion.SelectionChanged += (o, e) => { if(!fr) FilterJava(); };

            orderProperty.SelectionChanged += (o, e) => { if(!fr) OrderJava(null); };
            orderMode.SelectionChanged += (o, e) => { if(!fr) OrderJava(null); };
        }

        
        LevelDatInfo InfoFromChild(UIElement javacontchild) => (((javacontchild as Border).Child as EButton).Content as ContentControl).Content as LevelDatInfo;
        UIElement ChildFromInfo(LevelDatInfo level) {
            string dir = Path.Combine(Global.Settings.McDir, level.foldername);

            var b = new Border();
            b.BorderBrush = (Brush)FindResource("BORDER");
            var f = new ContentControl() { ContentTemplate = saveTemp, Content = level };
            f.Margin = new Thickness(0, 8, 0, 8);
            var btn = new EButton() { Background2 = new SolidColorBrush(Colors.Transparent) };
            btn.BorderThickness = new Thickness(0);
            btn.HorizontalContentAlignment = HorizontalAlignment.Stretch; btn.VerticalContentAlignment = VerticalAlignment.Top;
            btn.Content = f;

            b.Child = btn;
            b.BorderThickness = new Thickness(0, 0, 0, 1);

            btn.Click += (o, e) => {
                Global.App.OpenedSave = new Save(dir, level);

                opener_worlds.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            };

            return b;
        }

        Regex standardVersionRegex = new Regex("(1[.]\\d{1,2})[.]*.*");
        bool fr = false;
        void FilterJava() {
            string text = filterName.Text.ToLowerInvariant();
            string version = (string)filterVersion.SelectedItem;

            Regex versionregex = new Regex($"{version}.*");

            for(int i = 0; i < javaCont.Children.Count; i++) {
                var info = InfoFromChild(javaCont.Children[i]);
                bool hide = false;

                if(text != "") {
                    if(!info.name.ToLowerInvariant().Contains(text) && !info.foldername.ToLowerInvariant().Contains(text)) hide = true;
                }
                if(version == "All") {
                } else if(version == "Other") {
                    if(standardVersionRegex.Match(info.version_name).Success) hide = true;
                } else {
                    if(!versionregex.Match(info.version_name).Success) hide = true;
                }

                javaCont.Children[i].Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
            }
        }
        void OrderJava(List<(LevelDatInfo l, Visibility v)> levels) {
            string property = (string)orderProperty.SelectedItem;
            string mode = (string)orderMode.SelectedItem;

            if(levels == null) {
                levels = new List<(LevelDatInfo l, Visibility v)>();
                for(int i = 0; i < javaCont.Children.Count; i++) {
                    levels.Add((InfoFromChild(javaCont.Children[i]), javaCont.Children[i].Visibility));
                }
            }

            javaCont.Children.Clear();

            if(property == "Name") levels = levels.OrderBy(l => l.l.name).ThenBy(l => l.l.foldername).ToList();
            else if(property == "Version") levels = levels.OrderBy(l => l.l.version_id).ThenBy(l => l.l.foldername).ToList();
            else if(property == "Last open") levels = levels.OrderByDescending(l => l.l.lastopened).ThenBy(l => l.l.foldername).ToList();

            if(mode == "Desc") levels.Reverse();

            foreach(var n in levels) {
                var el = ChildFromInfo(n.l);
                javaCont.Children.Add(el);
                el.Visibility = n.v;
            }

        }



        
        public void OnActive() {          
            List<LevelDatInfo> levels = new();
            int br = 0;
            foreach(var dir in Global.FromFolder(Global.Settings.McDir, false, true).Shuffle()) {
                var level = LevelDatInfo.ReadWorld(dir);
                if(level == null) continue;

                levels.Add(level);
                br++;
            }

            javafilter.Visibility = br > 0 ? Visibility.Visible : Visibility.Collapsed;
            javaCont.Visibility = br > 0 ? Visibility.Visible : Visibility.Collapsed;

            emptyCont.Visibility = br == 0 ? Visibility.Visible : Visibility.Collapsed;
            //javaCont.Visibility = i != 0 ? Visibility.Visible : Visibility.Collapsed;

            List<string> options = new List<string>();
            for(int i = 0; i < levels.Count; i++) {
                var info = levels[i];
                var match = standardVersionRegex.Match(info.version_name);
                if(match.Success) {
                    options.Add(match.Groups[1].Value);
                }
            }
            options = options.Distinct().Order().ToList();
            options.Insert(0, "All");
            options.Add("Other");
            filterVersion.Items.Clear();
            foreach(var o in options) filterVersion.Items.Add(o);

            fr = true;
            filterName.Text = "";
            filterVersion.SelectedIndex = 0;
            orderProperty.SelectedIndex = 2;
            orderMode.SelectedIndex = 0;
            fr = false;

            OrderJava(levels.Select(l => (l, Visibility.Visible)).ToList());          
        }

    }
}
