using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace Mcasaenk.UI {
    /// <summary>
    /// Interaction logic for CustomDimensionSelectorWindow.xaml
    /// </summary>
    public partial class CustomDimensionSelectorWindow : Window {
        public CustomDimensionSelectorWindow(string[] dimensions, string current) {
            InitializeComponent();

            var currColor = (Color)ColorConverter.ConvertFromString("#70a0b2");
            var borderBrush = this.TryFindResource("BORDER") as SolidColorBrush;
            grid.RowDefinitions.Clear();
            for(int i = 0; i < dimensions.Length * 2 - 1; i++) {
                grid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

                if(i % 2 == 0) {
                    string dim = dimensions[i / 2];

                    EButton option = new EButton() { Height = 30, Padding = new Thickness(15, 0, 15, 0), BorderThickness = new Thickness(0) };
                    TextBlock text = new TextBlock();
                    text.Inlines.Add(new Run() { Text = dim.Split(':')[0] + ':', FontSize = 12 });
                    text.Inlines.Add(new Run() { Text = dim.Split(':')[1], FontSize = 14 });                   
                    if(dim == current) {
                        text.Foreground = new SolidColorBrush(currColor);
                        text.FontWeight = FontWeights.DemiBold;
                    }
                    option.Click += (a, b) => { result = dim; this.Close(); };
                    option.Content = text;
                    Grid.SetColumn(option, 0); Grid.SetColumnSpan(option, 3);
                    Grid.SetRow(option, i);
                    grid.Children.Add(option);
                } else {
                    Border sep = new Border() { Height = 1, BorderThickness = new Thickness(1), BorderBrush = borderBrush };
                    Grid.SetColumn(sep, 1);
                    Grid.SetRow(sep, i);
                    grid.Children.Add(sep);
                }
            }
            
        }

        private string result = "";
        public string Result() => result;
    }
}
