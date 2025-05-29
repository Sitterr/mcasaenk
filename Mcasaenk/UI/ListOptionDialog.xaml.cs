using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mcasaenk.UI {
    /// <summary>
    /// Interaction logic for CustomDimensionSelectorWindow.xaml
    /// </summary>
    public partial class ListOptionDialog : Window {
        public ListOptionDialog(string title, (TextBlock text, object data)[] options) {
            InitializeComponent();
            this.Title = title;


            var borderBrush = this.TryFindResource("BORDER") as SolidColorBrush;
            grid.RowDefinitions.Clear();
            for(int i = 0; i < options.Length * 2 - 1; i++) {
                grid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

                if(i % 2 == 0) {
                    var dim = options[i / 2];

                    EButton option = new EButton() { Height = 30, Padding = new Thickness(15, 0, 15, 0), BorderThickness = new Thickness(0) };
                    //TextBlock text = new TextBlock();
                    //text.Inlines.Add(new Run() { Text = dim.text.Split(':')[0] + ':', FontSize = 12 });
                    //text.Inlines.Add(new Run() { Text = dim.text.Split(':')[1], FontSize = 14 });                   
                    //if(dim.text == current) {
                    //    text.Foreground = new SolidColorBrush(currColor);
                    //    text.FontWeight = FontWeights.DemiBold;
                    //}
                    option.Click += (a, b) => { result = dim.data; this.Close(); };
                    option.Content = dim.text;
                    Grid.SetColumn(option, 0); Grid.SetColumnSpan(option, 3);
                    Grid.SetRow(option, i);
                    option.IsEnabled = dim.text.IsEnabled;
                    grid.Children.Add(option);
                } else {
                    Border sep = new Border() { Height = 1, BorderThickness = new Thickness(1), BorderBrush = borderBrush };
                    Grid.SetColumn(sep, 1);
                    Grid.SetRow(sep, i);
                    grid.Children.Add(sep);
                }
            }

        }

        private object result = null;
        public bool Result(out object data) {
            data = result;
            return result != null;
        }
    }
}
