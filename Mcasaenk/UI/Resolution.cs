using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;

namespace Mcasaenk.UI {
    public enum ResolutionType { stat, frame, resizeable, map, nul }
    public class Resolution : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

        private string _name;
        public string Name {
            get { return _name; }
            set {
                if(_name != value) {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        private int _x;
        public int X {
            get { return Math.Abs(_x); }
            set {
                if(_x != value) {
                    _x = value;
                    OnPropertyChanged(nameof(X));
                }
            }
        }

        private int _y;
        public int Y {
            get { return Math.Abs(_y); }
            set {
                if(_y != value) {
                    _y = value;
                    OnPropertyChanged(nameof(Y));
                }
            }
        }

        private bool _displaysize = true;
        [JsonIgnore]
        public bool DisplaySize {
            get { return _displaysize; }
            set {
                if(_displaysize != value) {
                    _displaysize = value;
                    OnPropertyChanged(nameof(DisplaySize));
                }
            }
        }

        private FontStyle _fontstyle = FontStyles.Normal;
        [JsonIgnore]
        public FontStyle FontStyle {
            get { return _fontstyle; }
            set {
                if(_fontstyle != value) {
                    _fontstyle = value;
                    OnPropertyChanged(nameof(FontStyle));
                }
            }
        }


        public ResolutionType type;


        public static Resolution screen = new Resolution() { Name = "Screen1", type = ResolutionType.stat, FontStyle = FontStyles.Oblique };
        public static Resolution custom = new Resolution() { Name = "Custom", type = ResolutionType.resizeable, X = 500, Y = 500, FontStyle = FontStyles.Oblique };
        public static Resolution map = new Resolution() { Name = "In-game map", type = ResolutionType.map, X = 128, Y = 128, FontStyle = FontStyles.Oblique };
        public static Resolution frame = new Resolution() { Name = "Frame", type = ResolutionType.frame, X = 5000, Y = 3000, FontStyle = FontStyles.Oblique };


        public static (int w, int h) CurrentResolution(Control control) {
            double screenWidthDIP = SystemParameters.PrimaryScreenWidth;
            double screenHeightDIP = SystemParameters.PrimaryScreenHeight;

            // Get the DPI scaling factor
            PresentationSource source = PresentationSource.FromVisual(control);
            double dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
            double dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;

            // Calculate the actual screen width and height in pixels
            int screenWidth = (int)Math.Round(screenWidthDIP * dpiX / 96.0);
            int screenHeight = (int)Math.Round(screenHeightDIP * dpiY / 96.0);

            return (screenWidth, screenHeight);
        }
    }

    public class ResolutionScale : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }


        private double _scale;
        public double Scale {
            get { return _scale; }
            set {
                if(_scale != value) {
                    _scale = value;
                    OnPropertyChanged(nameof(Scale));
                }
            }
        }
    }
}
