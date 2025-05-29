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
        public ColorPicker(WPFColor color) {
            InitializeComponent();

            this.inicolor = color;

            txt_r.Text = color.R.ToString();
            txt_g.Text = color.G.ToString();
            txt_b.Text = color.B.ToString();


            btn_transp.Click += (o, e) => {
                txt_r.Text = "";
                txt_g.Text = "";
                txt_b.Text = "";
            };

            txt_r.TextChanged += (o, e) => onChange();
            txt_g.TextChanged += (o, e) => onChange();
            txt_b.TextChanged += (o, e) => onChange();

            if(color.A == 0) btn_transp.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            else onChange();

            this.Closed += (o, e) => {
                if(programmaticallyClosed == false) rescolor = inicolor;
            };
        }

        public void OnSaveClick(object sender, RoutedEventArgs e) {
            programmaticallyClosed = true;
            this.Close();
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
