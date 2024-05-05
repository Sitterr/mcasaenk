using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Mcasaenk.UI
{
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
            get { return _x; }
            set {
                if(_x != value) {
                    _x = value;
                    OnPropertyChanged(nameof(X));
                }
            }
        }

        private int _y;
        public int Y {
            get { return _y; }
            set {
                if(_y != value) {
                    _y = value;
                    OnPropertyChanged(nameof(Y));
                }
            }
        }

        public static List<Resolution> predefined = new() {
            new Resolution() { Name = "-screen1-", X = 0, Y = 0 },
            new Resolution() { Name = "WXGA", X = 1280, Y = 720 },
            new Resolution() { Name = "HD", X = 1366, Y = 768 },
            new Resolution() { Name = "Full HD", X = 1920, Y = 1080 },
            new Resolution() { Name = "Quad HD", X = 2560, Y = 1440 },
            new Resolution() { Name = "4K UHD", X = 3840, Y = 2160 },
        };
        public static Resolution custom = new Resolution() { Name = "Custom", X = 100, Y = 100 };
        public static Resolution frame = new Resolution() { Name = "Frame", X = 5000, Y = 3000 };




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
}
