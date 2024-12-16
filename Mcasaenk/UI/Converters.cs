using Mcasaenk.WorldInfo;
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
    public class GreaterThanConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return System.Convert.ToDouble(value) > System.Convert.ToInt32(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
    public class LessThanConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return System.Convert.ToDouble(value) < System.Convert.ToInt32(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

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
            } else { 
                return value.Equals(parameter) ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class PlusNumberConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value is int intvalue) {
                if(intvalue == 0) return "";

                string a = $"+ {intvalue} {parameter}";
                if(intvalue > 1) a += "s";
                return a;
            } else if(value is bool boolvalue) {
                if(boolvalue == false) return "";
                else return $"+ a {parameter}";
            } else return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
    public class GamemodeColorConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value is not Gamemode gm) return "";

            return gm switch { 
                Gamemode.Survival => (SolidColorBrush)Application.Current.FindResource("LIGHT_YELLOW_B"),
                Gamemode.Hardcore => (SolidColorBrush)Application.Current.FindResource("LIGHT_RED_B"),
                Gamemode.Creative => (SolidColorBrush)Application.Current.FindResource("LIGHT_GREEN_B"),
                _ => (SolidColorBrush)Application.Current.FindResource("FORE"),
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return Math.Round((double)value, System.Convert.ToInt32(parameter));
        }
    }

    public class TransluciencyLevelTextConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value is not int n) return "";

            return n switch {
                0 => "none",
                1 => "depth only",
                2 => "1 level",
                _ => $"{n-1} levels",
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return Math.Round((double)value, System.Convert.ToInt32(parameter));
        }
    }

    public class PercentNumberReverseConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return Math.Round(1 - (System.Convert.ToDouble(value) / System.Convert.ToInt32(parameter)), 2).ToString("0.00");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return (int)Math.Round((1 - System.Convert.ToDouble(value)) * System.Convert.ToInt32(parameter));
        }
    }
    public class ReverseConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return System.Convert.ToInt32(parameter) - System.Convert.ToInt32(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return System.Convert.ToInt32(parameter) - System.Convert.ToInt32(value);
        }
    }


    public class ResolutionScaleTextToDouble : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value is double fr) return ConvertToFraction(fr);
            return "yes:yes";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value == null) return 1;
            if(value is ComboBoxItem) {
                if(((ComboBoxItem)value).Content is string str) {
                    var parts = str.Split(':');
                    return double.Parse(parts[0].Trim()) / double.Parse(parts[1].Trim());
                }
            } else if(value is string str) {
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
    public class BitAndConverter : IMultiValueConverter {
        public object Convert(object[] value, Type targetType, object _parameter, CultureInfo culture) {
            return value.Select(o => (bool)o).All(v => v);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            return [(bool)value];
        }
    }

    public class VisibilityMultConverter : IMultiValueConverter {
        public object Convert(object[] value, Type targetType, object _parameter, CultureInfo culture) {
            string parameter = "And";
            if(_parameter is string s) {  parameter = s; }

            bool res = true;
            if(parameter == "And") res = value.Select(o => (bool)o).All(v => v);
            else if(parameter == "Or") res = value.Select(o => (bool)o).Any(v => v);
            return res ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            return [(bool)value];
        }
    }

    public class DifferenceBoolConverter : IMultiValueConverter {
        public object Convert(object[] value, Type targetType, object _parameter, CultureInfo culture) {
            bool res = true;
            object v0 = value[0];
            for(int i = 1; i < value.Length; i++) res = res && v0.Equals(value[i]);
            return res;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class DifferenceConverter : IMultiValueConverter {
        public object Convert(object[] value, Type targetType, object parameter, CultureInfo culture) {
            if(parameter == null) parameter = (SolidColorBrush)Application.Current.FindResource("YELLOW_B");
            return value[0].Equals(value[1]) ? (SolidColorBrush)Application.Current.FindResource("FORE") : parameter;
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
    public class EnumToStringConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return ((Enum)value).Description();
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return null;
        }
    }
}
