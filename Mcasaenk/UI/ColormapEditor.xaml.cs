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
using System.ComponentModel;
using System.Drawing;
using static Mcasaenk.UI.ColormapEditor;
using System.Globalization;
using System.Data;
using System.Windows.Media.Animation;

// complete mess, pls dont look
namespace Mcasaenk.UI {
    /// <summary>
    /// Interaction logic for MakerStatus.xaml
    /// </summary>
    public partial class ColormapEditor : UserControl {
        private Brush redb, yellowb, checkerb2, checkerbh, checkerbp, borderb, light_blueb;
        private RawColormap rawcolormap;
        private Options options;
        private List<TextBlock> tintblockcounts, filterblockcounts;
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

            NoticeLabelConverter.yellowb = yellowb;
            NoticeLabelConverter.redb = redb;


            combo_filter.ItemsSource = new string[] { "All", "Notices", "Warnings", "Tips" };
            //combo_sort.ItemsSource = new string[] { "Name", "Notices" };

            combo_filter.SelectionChanged += (o, e) => Filter();
            txt_search.TextChanged += (o, e) => Filter();
            //combo_sort.SelectionChanged += (o, e) => {
            //    switch(combo_sort.SelectedItem) {
            //        case "Name":
            //            blockgrid.SortByColumn("BlockName", ListSortDirection.Ascending);
            //            break;

            //        case "Notices":
            //            blockgrid.SortByColumn("NoticesCombinedTypeValue", ListSortDirection.Descending);
            //            break;
            //    }
            //};

            btn_delete.Click += (o, e) => {
                var tobedeleted = blockgrid.SelectedItems.Cast<EditBlockRow>();
                blockgrid.ItemsSource = blockgrid.ItemsSource.Cast<EditBlockRow>().Where(bl => tobedeleted.Contains(bl) == false);

                for(int i = 0; i < rawcolormap.tints.Count; i++) {
                    tintblockcounts[i].Text = $"blocks: {rawcolormap.tints[i].blocks.Count}";
                }
                for(int i = 0; i < rawcolormap.filters.Count; i++) {
                    filterblockcounts[i].Text = $"blocks: {rawcolormap.filters[i].blocks.Count}";
                }
            };
            btn_delete.SetBinding(EButton.IsEnabledProperty, new Binding {
                    Source = blockgrid,
                    Path = new PropertyPath("SelectedItems.Count"),
                    Converter = new GreaterThanConverter(),
                    ConverterParameter = 0
                });

            btn_add.Click += (o, e) => {
                var dialog = ChooseNameDialog.AddBLockDialog(((IEnumerable<EditBlockRow>)blockgrid.ItemsSource).Select(bl => bl.BlockName).ToArray());
                dialog.ShowDialog();
                if(dialog.Result(out string newblock)) {
                    blockgrid.ItemsSource = ((IEnumerable<EditBlockRow>)blockgrid.ItemsSource).Append(new EditBlockRow(rawcolormap, newblock.minecraftname(), new RawBlock() { color = WPFColor.Transparent }));
                }
            };
        }
        public void Init(Action onClose) { this.onClose = onClose; }
        void Filter() {
            blockgrid.Items.Filter = ((Predicate<object>)(combo_filter.SelectedItem switch {
                "All" => (item => true),
                "Notices" => (item => ((EditBlockRow)item).NoticesCombinedTypeValue > 0),
                "Warnings" => (item => ((EditBlockRow)item).Notices.Any(not => not.type == ConstructedColormapNotice.Type.warning)),
                "Tips" => (item => ((EditBlockRow)item).Notices.Any(not => not.type == ConstructedColormapNotice.Type.tip)),
                _ => (item => true),
            })).And((item) => ((EditBlockRow)item).BlockName.Contains(txt_search.Text));

            blockgrid.SortByColumn("BlockName", ListSortDirection.Ascending);
        }
        public void Reset() {
            blockgrid.ItemsSource = null;
            tintgrid.RowDefinitions.Clear();
            tintgrid.Children.Clear();
            filtergrid.RowDefinitions.Clear();
            filtergrid.Children.Clear();
            tintblockcounts = null;
            filterblockcounts = null;
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

                blockgrid.ItemsSource = rawcolormap.blocks.Select(bl => {
                    return new EditBlockRow(rawcolormap, bl.Key, bl.Value);
                }).ToArray();

                combo_filter.SelectedIndex = 1;
                txt_search.Text = "";
            }

            {
                rawcolormap.tints = rawcolormap.tints.OrderByDescending(t => t.blocks.Count).ToList();

                int i = 0;
                tintblockcounts = new List<TextBlock>();
                foreach(var _tint in rawcolormap.tints) {
                    RawTint tint = _tint;
                    var format = TintMeta.GetFormat(tint.format);
                    tintgrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(32) });
                    tintgrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(32) });
                    tintgrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(10) });

                    Border imgborder = new Border() { BorderThickness = new Thickness(1), BorderBrush = borderb };
                    Image img = new Image();
                    //RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.);
                    if(tint.image != null) img.Source = tint.image.ToBitmapSource();
                    else img.Source = Global.CreateColorImageSource(tint.color.ToWinColor(), 64, 64);
                    imgborder.Child = img;

                    StackPanel info1 = new StackPanel() { VerticalAlignment = VerticalAlignment.Center },
                               info2 = new StackPanel() { VerticalAlignment = VerticalAlignment.Center };

                    var nametext = new TextBlock() { Text = $"name: {tint.name}" };
                    info1.Children.Add(nametext);

                    TextBlock formattext;
                    if(format != null) {
                        formattext = new TextBlock() { Text = $"format: {format.format}" };
                    } else {
                        formattext = new TextBlock() { Text = $"format: {format.format}", FontStyle = FontStyles.Italic, Foreground = redb };
                    }
                    info1.Children.Add(formattext);

                    {
                        var fore = (Brush)this.TryFindResource("FORE");
                        var fore_hover = (Brush)this.TryFindResource("FORE_HOVER");
                        var fore_press = (Brush)this.TryFindResource("FORE_PRESS");
                        var transp = new SolidColorBrush(Colors.Transparent);

                        var bltext = new LinkTextBlock() { };
                        bltext.TextBlock.Text = $"blocks: {tint.blocks.Count}";
                        tintblockcounts.Add(bltext.TextBlock);
                        bltext.PreviewMouseLeftButtonUp += (_, _) => {
                            // onclick
                            var vhodbiglist = blockgrid.ItemsSource.Cast<EditBlockRow>().Select(bl => {
                                BinaryBlockGroupWindow.Group group = BinaryBlockGroupWindow.Group.Def;
                                if(tint.blocks.Contains(bl.BlockName)) group = BinaryBlockGroupWindow.Group.This;
                                else {
                                    foreach(var tint in rawcolormap.tints) {
                                        if(tint.blocks.Contains(bl.BlockName)) {
                                            group = BinaryBlockGroupWindow.Group.Other;
                                            break;
                                        }
                                    }
                                }

                                return (bl.BlockName, bl.details?.shouldTint ?? false, group);
                            });
                            var window = new BinaryBlockGroupWindow(tint.name, vhodbiglist);
                            window.ShowDialog();
                            if(window.Result(out var otgbitlist)) {
                                var oldblocks = tint.blocks;
                                tint.blocks = otgbitlist.Where(f => f.group == BinaryBlockGroupWindow.Group.This).Select(f => f.name).ToList();

                                foreach(var block in oldblocks.Concat(tint.blocks)) {
                                    blockgrid.ItemsSource.Cast<EditBlockRow>().First(b => b.BlockName == block).UpdateNotices();
                                }
                                bltext.TextBlock.Text = $"blocks: {tint.blocks.Count}";
                                Filter();
                            }
                        };
                        info2.Children.Add(bltext);
                    }

                    Grid.SetColumn(imgborder, 0);
                    Grid.SetColumn(info1, 2);
                    Grid.SetColumn(info2, 2);

                    Grid.SetRow(imgborder, i);
                    Grid.SetRowSpan(imgborder, 2);
                    Grid.SetRow(info1, i);
                    Grid.SetRow(info2, i + 1);

                    tintgrid.Children.Add(imgborder);
                    tintgrid.Children.Add(info1);
                    tintgrid.Children.Add(info2);

                    i += 3;
                }
                if(tintgrid.RowDefinitions.Count > 0) tintgrid.RowDefinitions.RemoveAt(tintgrid.RowDefinitions.Count - 1);
            }


            {
                const int br_zavsekislu4aiFiltri = 3;
                RawFilter[] zavsekislu4aiFiltri = new RawFilter[br_zavsekislu4aiFiltri];
                for(int j = 0; j < br_zavsekislu4aiFiltri; j++) {
                    zavsekislu4aiFiltri[j] = new RawFilter() { blocks = new(), name = $"addgroup #{rawcolormap.filters.Count + j + 1}", transparency = 0 };
                }
                rawcolormap.filters = rawcolormap.filters.Concat(zavsekislu4aiFiltri).OrderByDescending(t => t.blocks.Count).ToList();

                int i = 0;
                filterblockcounts = new List<TextBlock>();
                foreach(var filter in rawcolormap.filters) {
                    filtergrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

                    var b1 = new Border() { BorderBrush = borderb, BorderThickness = new Thickness(1), Margin = new Thickness(0, 5, 0, 5) };
                    var t1 = new TextBlock() { Text = filter.name, FontSize = 14, Margin = new Thickness(0, 10, 0, 10), Padding = new Thickness(5), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    b1.Child = t1;

                    var b2 = new Border() { BorderBrush = borderb, BorderThickness = new Thickness(0, 1, 1, 1), Margin = new Thickness(0, 5, 0, 5) };
                    var t2 = new LinkTextBlock() { Margin = new Thickness(0, 10, 0, 10), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    filterblockcounts.Add(t2.TextBlock);
                    t2.TextBlock.Text = $"blocks: {filter.blocks.Count}";
                    b2.Child = t2;

                    t2.PreviewMouseLeftButtonUp += (_, _) => {
                        // onclick
                        var vhodbiglist = blockgrid.ItemsSource.Cast<EditBlockRow>().Select(bl => {
                            BinaryBlockGroupWindow.Group group = BinaryBlockGroupWindow.Group.Def;
                            if(filter.blocks.Contains(bl.BlockName)) group = BinaryBlockGroupWindow.Group.This;
                            else {
                                foreach(var filter in rawcolormap.filters) {
                                    if(filter.blocks.Contains(bl.BlockName)) {
                                        group = BinaryBlockGroupWindow.Group.Other;
                                        break;
                                    }
                                }
                            }

                            return (bl.BlockName, true, group);
                        });
                        var window = new BinaryBlockGroupWindow(filter.name, vhodbiglist);
                        window.ShowDialog();
                        if(window.Result(out var otgbitlist)) {
                            var oldblocks = filter.blocks;
                            filter.blocks = otgbitlist.Where(f => f.group == BinaryBlockGroupWindow.Group.This).Select(f => f.name).ToList();

                            foreach(var block in oldblocks.Concat(filter.blocks)) {
                                blockgrid.ItemsSource.Cast<EditBlockRow>().First(b => b.BlockName == block).UpdateNotices();
                            }
                            t2.TextBlock.Text = $"blocks: {filter.blocks.Count}";
                            Filter();
                        }
                    };

                    filtergrid.Children.Add(b1);
                    Grid.SetRow(b1, i);
                    Grid.SetColumn(b1, 0);
                    filtergrid.Children.Add(b2);
                    Grid.SetRow(b2, i);
                    Grid.SetColumn(b2, 1);

                    i++;
                }
            }

        }

        public void SetUp(bool showall, string path) {
            this.SetUp(RawColormap.Load(path), default, path, this.TryFindResource("FORE") as Brush, showall, path);
        }

        public void OnSave(object sender, RoutedEventArgs e) {
            if(savepath == "" || savepath == null) {
                string colormapspath = Path.Combine(Global.App.APPFOLDER, "colormaps");
                var nameinputdialog = ChooseNameDialog.SaveFileDialog(colormapspath, ["", ".zip"]);
                nameinputdialog.ShowDialog();
                if(nameinputdialog.Result(out string selectedpath)) {
                    savepath = Path.Combine(colormapspath, selectedpath);
                } else return;
            }

            rawcolormap.blocks.Clear();
            rawcolormap.blocks = (blockgrid.ItemsSource as IEnumerable<EditBlockRow>).Select(brow => new KeyValuePair<string, RawBlock>(brow.BlockName, new RawBlock() { color = brow.Color })).ToDictionary();

            RawColormap.Save(rawcolormap, savepath);
            MessageBox.Show("Colormap successfully saved!");
            onClose();
        }

        public delegate (List<(string main, string[] clarifications)> warnings, List<(string main, string[] clarifications)> tips) TipsMaker(string name, RawBlock block);
    }



    public class CButton : EButton {
        private static Brush checkerb2, checkerbh, checkerbp;
        static CButton() {
            checkerb2 = Global.CreateCheckerBrush(WPFColor.FromRgb(150).ToWinColor(), WPFColor.FromRgb(200).ToWinColor(), 8);
            checkerbh = Global.CreateCheckerBrush(WPFColor.FromRgb(175).ToWinColor(), WPFColor.FromRgb(225).ToWinColor(), 8);
            checkerbp = Global.CreateCheckerBrush(WPFColor.FromRgb(200).ToWinColor(), WPFColor.FromRgb(250).ToWinColor(), 8);

            ColorProperty = DependencyProperty.Register("Color", typeof(WPFColor), typeof(CButton), new PropertyMetadata(default(WPFColor), (d, e) => { ((CButton)d).OnColorChange(); }));
        }
        public CButton() {
            OnColorChange();
        }

        public static DependencyProperty ColorProperty;
        public WPFColor Color {
            get { return (WPFColor)base.GetValue(ColorProperty); }
            set { 
                base.SetValue(ColorProperty, value); 
            }
        }

        void OnColorChange() {
            if(Color.A == 0) {
                this.Background2 = checkerb2;
                this.BackgroundH = checkerbh;
                this.BackgroundP = checkerbp;
            } else {
                this.Background2 = new SolidColorBrush(Color.ToWinColor());
                this.BackgroundH = new SolidColorBrush(Color.Add(25).ToWinColor());
                this.BackgroundP = new SolidColorBrush(Color.Add(50).ToWinColor());
            }
            this.InvalidateVisual();
        }

        protected override void OnClick() {
            base.OnClick();

            var cdialog = new ColorPicker(Color);
            cdialog.ShowDialog();
            WPFColor res = cdialog.GetResult();
            if(Color != res) Color = res;
        }
    }

    public class LinkTextBlock : Border {
        private Brush fore, fore_hover, fore_press, transp;
        public LinkTextBlock() {
            fore = (Brush)this.TryFindResource("FORE");
            fore_hover = (Brush)this.TryFindResource("FORE_HOVER");
            fore_press = (Brush)this.TryFindResource("FORE_PRESS");
            transp = new SolidColorBrush(Colors.Transparent);

            this.Child = TextBlock = new TextBlock();
            this.BorderThickness = new Thickness(0, 0, 0, 1);
            this.BorderBrush = fore;
            this.HorizontalAlignment = HorizontalAlignment.Left;

            this.MouseEnter += (o, e) => {
                this.BorderBrush = fore_hover;
                TextBlock.Foreground = fore_hover;
            };
            this.MouseLeave += (o, e) => {
                this.BorderBrush = fore;
                TextBlock.Foreground = fore;
            };
            this.MouseLeftButtonDown += (o, e) => {
                this.BorderBrush = fore_press;
                TextBlock.Foreground = fore_press;
            };
            this.PreviewMouseLeftButtonUp += (o, e) => {
                this.BorderBrush = fore_hover;
                TextBlock.Foreground = fore_hover;
            };
        }

        public readonly TextBlock TextBlock;
    }

    public class EditBlockRow : INotifyPropertyChanged {
        private readonly RawColormap colormap;
        public readonly CreationDetails details;
        private bool initit = true;
        public EditBlockRow(RawColormap colormap, string name, RawBlock rawBlock) {
            this.colormap = colormap;
            this.BlockName = name;
            this.Color = rawBlock.color;
            this.details = rawBlock.details;

            initit = false;
            this.UpdateNotices();
        }


        private string blockname;
        public string BlockName {
            get => blockname;
            set {
                blockname = value;
                OnPropertyChanged(nameof(BlockName));
                //this.UpdateNotices();
            }
        }

        private WPFColor color;
        public WPFColor Color {
            get => color;
            set {
                color = value;
                OnPropertyChanged(nameof(Color));
                this.UpdateNotices();
            }
        }

        public void UpdateNotices() {
            if(initit) return;
            Notices = ConstructedColormapNotice.MakeNotices(Color, details, colormap.tints.Any(t => t.blocks.Contains(BlockName)));
            OnPropertyChanged(nameof(Notices));
            OnPropertyChanged(nameof(NoticesCombinedTypeValue));
        }
        public List<ConstructedColormapNotice> Notices { get; set; }
        public int NoticesCombinedTypeValue => Notices?.Sum(n => (int)n.type) ?? 0;











        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }

    public class NoticeLabelConverter : IValueConverter {
        public static Brush yellowb, redb;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            Label label = new Label();
            if(value is not List<ConstructedColormapNotice> notices) return null;
            if(notices.Count == 0) return null;


            var mosturgenttype = (ConstructedColormapNotice.Type)notices.Max(n => (int)n.type);
            if(mosturgenttype == ConstructedColormapNotice.Type.warning) label.Content = new TextBlock() { Text = "⚠", Foreground = redb };
            else if(mosturgenttype == ConstructedColormapNotice.Type.tip) label.Content = new TextBlock() { Text = "❗", Foreground = yellowb };


            StackPanel stack = new StackPanel();
            foreach(var notice in notices) {
                if(notice.message == "") continue;

                stack.Children.Add(new TextBlock() { Text = "• " + notice.message });
                foreach(var clar in notice.clarifications) {
                    stack.Children.Add(new TextBlock() { Text = "   • " + clar, FontSize = 10 });
                }
            }
            label.ToolTip = stack;

            return label;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}