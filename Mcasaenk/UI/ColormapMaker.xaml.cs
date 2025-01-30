using Accessibility;
using Mcasaenk.Resources;
using Mcasaenk.WorldInfo;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using WPF.ImageEffects;

namespace Mcasaenk.UI {
    /// <summary>
    /// Interaction logic for ColormapMaker.xaml
    /// </summary>
    public partial class ColormapMaker : UserControl {
        PackItemsControl list_available, list_selected;
        public ColormapMaker() {
            InitializeComponent();

            list_available = new PackItemsControl((DataTemplate)this.TryFindResource("packmeta"), true, (i, p) => {
                Debug.Assert(p == "right");
                var item = list_available.OItems[i];
                list_available.OItems.Remove(item);
                list_selected.OItems.Insert(0, item);
            });
            group_available.Content = list_available;

            list_selected = new PackItemsControl((DataTemplate)this.TryFindResource("packmeta"), false, (i, p) => {
                if(p == "left") {
                    var item = list_selected.OItems[i];
                    list_selected.OItems.Remove(item);
                    list_available.OItems.Add(item);
                } else if(p == "downright") {
                    var temp = list_selected.OItems[i];
                    list_selected.OItems[i] = list_selected.OItems[i + 1];
                    list_selected.OItems[i + 1] = temp;
                } else if(p == "upright") {
                    var temp = list_selected.OItems[i];
                    list_selected.OItems[i] = list_selected.OItems[i - 1];
                    list_selected.OItems[i - 1] = temp;
                }
            });
            group_selected.Content = list_selected;
        }
        private Action onClose;
        public void Init(Action onClose) { this.onClose = onClose; }

        public void SetUp() {
            list_available.OItems.Clear();
            list_selected.OItems.Clear();

            foreach(var fileorfolder in Global.FromFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "resourcepacks"), true, true)) {
                using var read = ReadInterface.GetSuitable(fileorfolder);
                if(PackMetadata.ReadPackMeta(read, Global.ReadName(fileorfolder), out var meta) == false) continue;
                list_available.OItems.Add(meta);
            }

            string defpath = Path.Combine(Global.App.APPFOLDER, "vanilla_resource_pack.zip");
            if(File.Exists(defpath)) list_selected.OItems.Add(new PackMetadata(defpath, "Default", WPFBitmap.FromBytes(ResourceMapping.default_pack).ToBitmapSource(), "The default look and feel of Minecraft"));
        }

        public void OnAddManuallyMod(object sender, RoutedEventArgs e) {
            var dialog = new Microsoft.Win32.OpenFileDialog() {
                FileName = "",
                DefaultExt = ".jar",
                Filter = "Jar mods (.jar)|*.jar",
                InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "mods"),
                Multiselect = true,
            };

            if(dialog.ShowDialog() == true) {
                foreach(var file in dialog.FileNames) {
                    using var read = new ZipRead(file);
                    if(PackMetadata.ReadModMeta(read, out var meta)) {
                        if(!list_selected.OItems.Any(m => m.id == meta.id)) list_selected.OItems.Insert(0, meta);
                    }
                }
            }
        }
        public void OnAddManuallyPack(object sender, RoutedEventArgs e) {
            var dialog = new Microsoft.Win32.OpenFileDialog() {
                FileName = "",
                DefaultExt = ".zip",
                Filter = "Zip file (.zip)|*.zip",
                Multiselect = true,
            };

            if(dialog.ShowDialog() == true) {
                foreach(var file in dialog.FileNames) {
                    using var read = new ZipRead(file);
                    if(PackMetadata.ReadPackMeta(read, Path.GetFileName(file), out var meta)) {
                        list_selected.OItems.Insert(0, meta);
                    }
                }
            }
        }


        public void OnDone(object sender, RoutedEventArgs e) {
            this.onClose();
        }



        public string[] GetResult() => list_selected.OItems.Select(x => x.path).Reverse().ToArray();
    }













    class PackItemsControl : ItemsControl {
        private readonly bool leftbased;
        private readonly Action<int, string> onClick;
        public PackItemsControl(DataTemplate template, bool leftbased, Action<int, string> onClick) {
            this.leftbased = leftbased;
            this.Margin = new Thickness(10, 5, 10, 5);
            this.ItemTemplate = template;
            this.OItems = new ObservableCollection<PackMetadata>();
            this.onClick = onClick;

            OItems.CollectionChanged += PackItemsControl_CollectionChanged;
            this.ItemsSource = OItems;

            //ItemContainerGenerator.ItemsChanged
        }

        public readonly ObservableCollection<PackMetadata> OItems;
         
        private void PackItemsControl_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
            Dispatcher.BeginInvoke(new Action(() => {
                for(int _i = 0; _i < Items.Count; _i++) {
                    int i = _i;
                    var item = Items[i] as PackMetadata;
                    var container = ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;

                    var arrow = FindVisualChild<ArrowButton>(container, "btn_arrow");
                    arrow.onClick = (posoka) => this.onClick(i, posoka);
                    arrow.RightArrow = false;
                    arrow.LeftArrow = false;
                    arrow.DownRightArrow = false;
                    arrow.UpRightArrow = false;
                    if(leftbased) {
                        arrow.RightArrow = true;
                    } else {
                        arrow.LeftArrow = true;
                        if(i > 0) arrow.UpRightArrow = true;
                        if(i < Items.Count - 1) arrow.DownRightArrow = true;
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }



        private static T FindVisualChild<T>(DependencyObject parent, string childName) where T : DependencyObject {
            // Confirm parent and type
            if(parent == null) return null;
            T foundChild = null;
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for(int i = 0; i < childrenCount; i++) {
                var child = VisualTreeHelper.GetChild(parent, i);
                if(child is T typedChild && (child as FrameworkElement)?.Name == childName) {
                    foundChild = typedChild;
                    break;
                }
                foundChild = FindVisualChild<T>(child, childName);
                if(foundChild != null) break;
            }
            return foundChild;
        }
    }

    class ArrowButton : Button {
        static ArrowButton() {
            RightArrowProperty = DependencyProperty.Register("RightArrow", typeof(bool), typeof(ArrowButton));
            LeftArrowProperty = DependencyProperty.Register("LeftArrow", typeof(bool), typeof(ArrowButton));
            DownRightArrowProperty = DependencyProperty.Register("DownRightArrow", typeof(bool), typeof(ArrowButton));
            UpRightArrowProperty = DependencyProperty.Register("UpRightArrow", typeof(bool), typeof(ArrowButton));
        }

        public static DependencyProperty RightArrowProperty;
        public bool RightArrow {
            get { return (bool)base.GetValue(RightArrowProperty); }
            set { if(RightArrow != value) base.SetValue(RightArrowProperty, value); }
        }
        public static DependencyProperty LeftArrowProperty;
        public bool LeftArrow {
            get { return (bool)base.GetValue(LeftArrowProperty); }
            set { if(LeftArrow != value) base.SetValue(LeftArrowProperty, value);}
        }
        public static DependencyProperty DownRightArrowProperty;
        public bool DownRightArrow {
            get { return (bool)base.GetValue(DownRightArrowProperty); }
            set { if(DownRightArrow != value) base.SetValue(DownRightArrowProperty, value); }
        }
        public static DependencyProperty UpRightArrowProperty;
        public bool UpRightArrow {


            get { return (bool)base.GetValue(UpRightArrowProperty); }
            set { if(UpRightArrow != value) base.SetValue(UpRightArrowProperty, value); }
        }

        private EButton rightbtn, leftbtn, uprightbtn, downrightbtn;

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            rightbtn = GetTemplateChild("_right_") as EButton;
            leftbtn = GetTemplateChild("_left_") as EButton;
            uprightbtn = GetTemplateChild("_upright_") as EButton;
            downrightbtn = GetTemplateChild("_downright_") as EButton;

            var isMouseOverBind = new Binding("IsMouseOver") { Source = this };

            {
                var rv = new MultiBinding() { Converter = new VisibilityMultConverter(), Mode = BindingMode.OneWay, ConverterParameter = "And" };
                rv.Bindings.Add(new Binding("RightArrow") { Source = this });
                rv.Bindings.Add(isMouseOverBind);
                rightbtn.SetBinding(EButton.VisibilityProperty, rv);

                rightbtn.Click += (o, e) => onClick("right");
            }

            {
                var rv = new MultiBinding() { Converter = new VisibilityMultConverter(), Mode = BindingMode.OneWay, ConverterParameter = "And" };
                rv.Bindings.Add(new Binding("LeftArrow") { Source = this });
                rv.Bindings.Add(isMouseOverBind);
                leftbtn.SetBinding(EButton.VisibilityProperty, rv);

                leftbtn.Click += (o, e) => onClick("left");
            }

            {
                var rv = new MultiBinding() { Converter = new VisibilityMultConverter(), Mode = BindingMode.OneWay, ConverterParameter = "And" };
                rv.Bindings.Add(new Binding("UpRightArrow") { Source = this });
                rv.Bindings.Add(isMouseOverBind);
                uprightbtn.SetBinding(EButton.VisibilityProperty, rv);

                uprightbtn.Click += (o, e) => onClick("upright");
            }

            {
                var rv = new MultiBinding() { Converter = new VisibilityMultConverter(), Mode = BindingMode.OneWay, ConverterParameter = "And" };
                rv.Bindings.Add(new Binding("DownRightArrow") { Source = this });
                rv.Bindings.Add(isMouseOverBind);
                downrightbtn.SetBinding(EButton.VisibilityProperty, rv);

                downrightbtn.Click += (o, e) => onClick("downright");
            }
        }

        public Action<string> onClick;

        protected override void OnMouseEnter(MouseEventArgs e) {
            base.OnMouseEnter(e);

            if(this.Content is Image img) {
                img.Effect = new BrightnessContrastEffect() { Brightness = 0.06, Contrast = 0.05 };
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e) {
            base.OnMouseLeave(e);

            if(this.Content is Image img) {
                img.Effect = new BrightnessContrastEffect() { Brightness = 0, Contrast = 0 };
            }
        }
    }
}
