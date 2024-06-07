using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Mcasaenk.UI
{

    public class RoundConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value is double doubleValue) {
                if(parameter == null) parameter = 2;
                else parameter = System.Convert.ToInt32(parameter);
                return doubleValue.ToString("0." + new string('0', (int)parameter));
            }
            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return Math.Round((double)value, System.Convert.ToInt32(parameter));
        }
    }

    public class VisibilityConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value is bool visibility) {
                return visibility ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class ResolutionScaleTextToDouble : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value is double fr) return ConvertToFraction(fr);
            return "yes:yes";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {

            if(((ComboBoxItem)value).Content is string str) {
                var parts = str.Split(':');
                return double.Parse(parts[0].Trim()) / double.Parse(parts[1].Trim());
            }
            return -1;

        }


        static string ConvertToFraction(double number) {
            int sign = Math.Sign(number);
            number = Math.Abs(number);

            const double epsilon = 1e-10; // A small tolerance to account for floating point inaccuracies
            double numerator = number;
            double denominator = 1;

            while(Math.Abs(number * denominator - Math.Round(number * denominator)) > epsilon) {
                denominator++;
            }

            numerator = Math.Round(number * denominator);

            int gcd = GCD((int)numerator, (int)denominator);

            numerator /= gcd;
            denominator /= gcd;

            return $"{sign * (int)numerator}:{(int)denominator}";
        }

        static int GCD(int a, int b) {
            while(b != 0) {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }
    }

    public class BitOrConverter : IMultiValueConverter {
        public object Convert(object[] value, Type targetType, object parameter, CultureInfo culture) {
            return value.Select(o => (bool)o).Any(v => v);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class DifferenceConverter : IMultiValueConverter {
        public object Convert(object[] value, Type targetType, object parameter, CultureInfo culture) {
            return value[0].Equals(value[1]) ? (SolidColorBrush)Application.Current.FindResource("FORE") : (SolidColorBrush)Application.Current.FindResource("YELLOW_B");
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
    public class StarConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return (bool)value ? "✶" : "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class FooterConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return (bool)value ? new GridLength(25) : new GridLength(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(Enum), typeof(IEnumerable<ValueDescription>))]
    public class EnumToCollectionConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return EnumHelper.GetAllValuesAndDescriptions(value.GetType());
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return null;
        }
    }
}
