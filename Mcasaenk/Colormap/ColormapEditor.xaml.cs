using Mcasaenk.Colormaping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Documents;
using System.Net;
using System.Windows.Media.Imaging;
using System.Net.NetworkInformation;
using Microsoft.Win32;

// complete mess, pls dont look
namespace Mcasaenk.UI {
    /// <summary>
    /// Interaction logic for MakerStatus.xaml
    /// </summary>
    public partial class ColormapEditor : UserControl {
        private Brush redb, yellowb, checkerb2, checkerbh, checkerbp, borderb, light_blueb;
        private RawColormap rawcolormap;
        private Options options;
        private List<TextBlock> tintblockcounts;
        private Action onClose;
        public ColormapEditor() {
            InitializeComponent();
            redb = this.TryFindResource("RED_B") as Brush;
            yellowb = this.TryFindResource("YELLOW_B") as Brush;
            borderb = this.TryFindResource("BORDER") as Brush;
            light_blueb = this.TryFindResource("LIGHT_BLUE_B") as Brush;

            checkerb2 = Global.CreateCheckerBrush(WPFColor.FromRgb(150).ToWinColor(), WPFColor.FromRgb(200).ToWinColor());
            checkerbh = Global.CreateCheckerBrush(WPFColor.FromRgb(175).ToWinColor(), WPFColor.FromRgb(225).ToWinColor());
            checkerbp = Global.CreateCheckerBrush(WPFColor.FromRgb(200).ToWinColor(), WPFColor.FromRgb(250).ToWinColor());
        }
        public void Init(Action onClose) { this.onClose = onClose; }

        public void Reset() {
            for(int i = 2; i < blgrid.RowDefinitions.Count; i++) {
                blgrid.RowDefinitions.RemoveAt(2);
            }
            tintgrid.RowDefinitions.Clear();
            tintblockcounts = null;
        }

        public string savepath;

        public void SetUp(RawColormap rawcolormap, Options options, string title, Brush titleColor, bool showall, string savepath = null) {
            //this.Dispatcher.Invoke(new Action(() => { 
            this.Reset();
            this.savepath = savepath;
            this.rawcolormap = rawcolormap;
            this.options = options;
            txt_title.Foreground = titleColor;
            txt_title.Text = title;

            {
                rawcolormap.tints = rawcolormap.tints.OrderByDescending(t => t.blocks.Count).ToList();

                rawcolormap.blocks = rawcolormap.blocks.OrderByDescending((bl) => {
                    int res = 0;
                    var warningtips = tipsFor(bl.Key, bl.Value);
                    res += warningtips.warnings.Sum(w => w.main.Length) * 1000 + warningtips.tips.Sum(w => w.main.Length) * 10;
                    if(rawcolormap.tints.Any(t => t.blocks.Contains(bl.Key))) res += 1;
                    return res;
                }).ThenBy(bl => bl.Key).ToDictionary();
            }

            {
                int i = 2;
                var tints = new string[] { "" }.Concat(rawcolormap.tints.Select(t => t.name));
                var colorwidthbinding = new Binding { RelativeSource = new RelativeSource(RelativeSourceMode.Self), Path = new PropertyPath("ActualHeight") };


                foreach(var block in rawcolormap.blocks) {
                    var warningstips = tipsFor(block.Key, block.Value);
                    if(showall == false && warningstips.warnings.Count == 0 && warningstips.tips.Count == 0 && rawcolormap.tints.Any(t => t.blocks.Contains(block.Key)) == false) continue;

                    blgrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                    blgrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(3) });
                    StackPanel idstatus = new StackPanel() { Orientation = Orientation.Horizontal };
                    TextBlock id = new TextBlock() { Text = block.Key.simplifyminecraftname() };
                    Label status_warning = new Label() { Margin = new Thickness(3, 0, 0, 0) };
                    Label status_tips = new Label() { Margin = new Thickness(3, 0, 0, 0) };
                    idstatus.Children.Add(id);
                    idstatus.Children.Add(status_warning);
                    idstatus.Children.Add(status_tips);
                    EButton color = new EButton();
                    if(block.Value.color.A == 0) {
                        color.Background2 = checkerb2;
                        color.BackgroundH = checkerbh;
                        color.BackgroundP = checkerbp;
                    } else {
                        color.Background2 = new SolidColorBrush(block.Value.color.ToWinColor());
                        color.BackgroundH = new SolidColorBrush(block.Value.color.Add(25).ToWinColor());
                        color.BackgroundP = new SolidColorBrush(block.Value.color.Add(50).ToWinColor());
                    }
                    color.SetBinding(WidthProperty, colorwidthbinding);
                    ComboBox combo_tint = new ComboBox();
                    combo_tint.HorizontalContentAlignment = HorizontalAlignment.Center;
                    if(block.Value.details == null) {
                        combo_tint.Visibility = Visibility.Visible;
                    } else {
                        combo_tint.Visibility = block.Value.details.shouldTint ? Visibility.Visible : Visibility.Hidden;
                    }
                    if(combo_tint.Visibility == Visibility.Visible) {
                        combo_tint.ItemsSource = tints;
                        var t = rawcolormap.tints.FirstOrDefault(t => t.blocks.Contains(block.Key));
                        if(t != null) combo_tint.SelectedItem = t.name;
                        else combo_tint.SelectedIndex = 0;
                    }


                    combo_tint.SelectionChanged += (a, b) => {
                        foreach(var tint in rawcolormap.tints) {
                            tint.blocks.Remove(block.Key);
                        }
                        if(combo_tint.SelectedIndex > 0) {
                            rawcolormap.tints[combo_tint.SelectedIndex - 1].blocks.Add(block.Key);
                        }
                        for(int i = 0; i < rawcolormap.tints.Count; i++) {
                            tintblockcounts[i].Text = $"blocks: {rawcolormap.tints[i].blocks.Count}";
                        }

                        onBlockChange(status_warning, status_tips, block.Key, block.Value);
                    };
                    color.Click += (a, b) => {
                        var cdialog = new ColorPicker(block.Value.color);
                        cdialog.ShowDialog();
                        WPFColor res = cdialog.GetResult();
                        if(block.Value.color == res) return;
                        else block.Value.color = res;

                        if(block.Value.color.A == 0) {
                            color.Background2 = checkerb2;
                            color.BackgroundH = checkerbh;
                            color.BackgroundP = checkerbp;
                        } else {
                            color.Background2 = new SolidColorBrush(block.Value.color.ToWinColor());
                            color.BackgroundH = new SolidColorBrush(block.Value.color.Add(25).ToWinColor());
                            color.BackgroundP = new SolidColorBrush(block.Value.color.Add(50).ToWinColor());
                        }

                        onBlockChange(status_warning, status_tips, block.Key, block.Value);
                    };


                    onBlockChange(status_warning, status_tips, block.Key, block.Value, warningstips.warnings, warningstips.tips);

                    Grid.SetColumn(idstatus, 0);
                    Grid.SetColumn(color, 1);
                    Grid.SetColumn(combo_tint, 2);

                    Grid.SetRow(idstatus, i);
                    Grid.SetRow(color, i);
                    Grid.SetRow(combo_tint, i);

                    blgrid.Children.Add(idstatus);
                    blgrid.Children.Add(color);
                    blgrid.Children.Add(combo_tint);

                    i += 2;
                }
            }

            {
                int i = 0;
                tintblockcounts = new List<TextBlock>();
                foreach(var tint in rawcolormap.tints) {
                    var format = TintFormat.GetFormat(tint.format);
                    tintgrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(64) });
                    tintgrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(10) });

                    Border imgborder = new Border() { BorderThickness = new Thickness(1), BorderBrush = borderb };
                    Image img = new Image();
                    //RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.);
                    if(tint.image != null) img.Source = tint.image.ToBitmapSource();
                    else img.Source = Global.CreateColorImageSource(tint.color.ToWinColor(), 64, 64);
                    imgborder.Child = img;

                    StackPanel info = new StackPanel() { VerticalAlignment = VerticalAlignment.Center };
                    info.Children.Add(new TextBlock() { Text = $"name: {tint.name}" });
                    if(format != null) {
                        info.Children.Add(new TextBlock() { Text = $"format: {format.format}" });
                    } else {
                        info.Children.Add(new TextBlock() { Text = $"format: {format.format}", FontStyle = FontStyles.Italic, Foreground = redb });
                    }
                    tintblockcounts.Add(new TextBlock() { Text = $"blocks: {tint.blocks.Count}" });
                    info.Children.Add(tintblockcounts.Last());

                    Grid.SetColumn(imgborder, 0);
                    Grid.SetColumn(info, 2);

                    Grid.SetRow(imgborder, i);
                    Grid.SetRow(info, i);

                    tintgrid.Children.Add(imgborder);
                    tintgrid.Children.Add(info);

                    i += 2;
                }
            }
            //}));
        }

        private void onBlockChange(Label status_warning, Label status_tips, string blockname, RawBlock block, List<(string main, string[] clarifications)> warnings = null, List<(string main, string[] clarifications)> tips = null) {
            if(tips == null || warnings == null) (warnings, tips) = tipsFor(blockname, block);

            if(warnings.Count > 0) {
                if(status_warning.Content is not TextBlock) {
                    status_warning.Content = new TextBlock() { Text = "⚠", Foreground = redb };

                    StackPanel stack = new StackPanel();
                    foreach(var warn in warnings) {
                        stack.Children.Add(new TextBlock() { Text = "• " + warn.main });
                        foreach(var clar in warn.clarifications) {
                            stack.Children.Add(new TextBlock() { Text = "   • " + clar, FontSize = 10 });
                        }
                    }
                    status_warning.ToolTip = stack;
                }
            } else {
                status_warning.Content = "";
            }

            if(tips.Count > 0) {
                if(status_tips.Content is not TextBlock) {
                    status_tips.Content = new TextBlock() { Text = "❗", Foreground = yellowb };

                    StackPanel stack = new StackPanel();
                    foreach(var tip in tips) {
                        stack.Children.Add(new TextBlock() { Text = "• " + tip.main });
                        foreach(var clar in tip.clarifications) {
                            stack.Children.Add(new TextBlock() { Text = "   • " + clar, FontSize = 10 });
                        }
                    }
                    status_tips.ToolTip = stack;
                }
            } else {
                status_tips.Content = "";
            }
        }
        private (List<(string main, string[] clarifications)> warnings, List<(string main, string[] clarifications)> tips) tipsFor(string name, RawBlock block) {
            List<(string main, string[] clarifications)> warnings = new(), tips = new();
            if(block.color.A < 255) warnings.Add(("the color of this block is transparent and thus will not be rendered", ["many times it is this for a reason and not just an error"]));
            if(block.details != null) {
                if(block.details.shouldTint) {
                    int ti = 0;
                    for(; ti < rawcolormap.tints.Count; ti++) {
                        if(rawcolormap.tints[ti].blocks.Contains(name)) {
                            break;
                        }
                    }
                    if(ti == rawcolormap.tints.Count) {
                        warnings.Add(("the model of this block suggests that the block be somehow tinted", ["sometimes this warning is false positive and the block doesn't need tint", "if the base color is greyish, then it most certainly does need to be tinted"]));
                    }
                }

                if(block.details.creationMethod == BlockCreationMethod.Texture && options.minQ > 0) {
                    tips.Add(("this block lacked a proper model, but the generator succeeded in finding a texture", []));
                }
            }
            return (warnings, tips);
        }

        public void SetUp(bool showall, string path) {
            this.SetUp(RawColormap.Load(path), default, path, this.TryFindResource("FORE") as Brush, showall, path);
        }



        public void OnSave(object sender, RoutedEventArgs e) {
            if(savepath == "" || savepath == null) {
                string colormapspath = Path.Combine(Global.App.APPFOLDER, "colormaps");
                var nameinputdialog = new ChooseNameDialog(colormapspath, ["", ".zip"]);
                nameinputdialog.ShowDialog();
                if(nameinputdialog.Result(out string selectedpath)) {
                    savepath = Path.Combine(colormapspath, selectedpath);
                } else return;
            }

            RawColormap.Save(rawcolormap, savepath);
            MessageBox.Show("Colormap successfully saved!");
            onClose();
        }

    }
}
