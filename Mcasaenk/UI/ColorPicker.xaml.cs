using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mcasaenk.UI {
    /// <summary>
    /// Interaction logic for ColorPicker.xaml
    /// </summary>
    public partial class ColorPicker : Window {
        private WPFColor inicolor, rescolor;
        private bool programmaticallyClosed = false;
        private IEnumerable<(string name, bool important, BinaryBlockGroupWindow.Group group)> samples;
        private Dictionary<string, WPFColor> samplecolors;
        public ColorPicker(WPFColor color, Dictionary<string, WPFColor> samples, string title = "") {
            InitializeComponent();
            img.Background = Global.CreateCheckerBrush(Color.FromRgb(150, 150, 150), Color.FromRgb(200, 200, 200));

            if(title != "") this.Title = title;

            this.samplecolors = samples;
            this.samples = samples.Select(b => (b.Key, true, BinaryBlockGroupWindow.Group.Def));
            this.inicolor = color;

            btn_transp.Click += (o, e) => {
                SetColor(WPFColor.Transparent);
            };

            txt_r.TextChanged += (o, e) => onChange();
            txt_g.TextChanged += (o, e) => onChange();
            txt_b.TextChanged += (o, e) => onChange();


            SetColor(color);

            this.Closed += (o, e) => {
                if(programmaticallyClosed == false) rescolor = inicolor;
            };
        }

        private void SetColor(WPFColor color) {
            if(color.A == 255) {
                txt_r.Text = color.R.ToString();
                txt_g.Text = color.G.ToString();
                txt_b.Text = color.B.ToString();
            } else {
                txt_r.Text = "";
                txt_g.Text = "";
                txt_b.Text = "";
            }
        }

        public void OnSaveClick(object sender, RoutedEventArgs e) {
            programmaticallyClosed = true;
            this.Close();
        }

        public void OnCopyFromClick(object sender, RoutedEventArgs e) {
            var d = new BinaryBlockGroupWindow("Available", samples, true);
            d.ShowDialog();
            if(d.Result(out var l)) {
                var q = l.FirstOrDefault(l => l.group == BinaryBlockGroupWindow.Group.This);
                if(q != default) {
                    string name = q.name;
                    var res = samplecolors.First(s => s.Key == name).Value;
                    this.SetColor(res);
                }
            }
        }

        public WPFColor GetResult() => rescolor;

        void onChange() {
            try {
                byte r = byte.Parse(txt_r.Text);
                byte g = byte.Parse(txt_g.Text);
                byte b = byte.Parse(txt_b.Text);

                rescolor = new WPFColor(r, g, b);
                img.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
            } catch {
                rescolor = WPFColor.Transparent;
                img.Background = Global.CreateCheckerBrush(Color.FromRgb(150, 150, 150), Color.FromRgb(200, 200, 200));
            }
            //img.Source = createColorImageSource();
        }
    }
}
