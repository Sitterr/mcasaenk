using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Mcasaenk {
    public class Global {
        public static Random rand = new Random();


        public static Color FromArgb(double alpha, Color baseColor) {
            return Color.FromArgb((byte)(alpha * 256), baseColor.R, baseColor.G, baseColor.B);
        }



        public static class Coord {
            public static double absDev(double a, int b) {
                a = Math.Floor(a);
                int res = (int)a / b;
                if(a < 0) {
                    res = ((int)(a + 1) / b) - 1;
                }
                return res;
            }
            public static double absMod(double a, int m) {
                double res = a % m;
                if(res < 0) {
                    res = m + res;
                }
                return res;
            }
        }


        public class ColorPallete {
            public static readonly ColorPallete Pallete = new ColorPallete(2, (float)75/100);

            private float ration_red;
            private float ration_green;
            private float ration_blue;

            private ColorPallete(int mainColor, float mainRatio) {
                ration_red = mainRatio;
                ration_green = mainRatio;
                ration_blue = mainRatio;
                switch(mainColor) { 
                    case 0:
                    ration_red = 1 / ration_red;
                    break;
                    case 1:
                    ration_green = 1 / ration_green;
                    break;
                    case 2:
                    ration_blue = 1 / ration_blue;
                    break;
                }
            }

            private Color Color(int value) {
                return System.Windows.Media.Color.FromRgb((byte)Math.Min(255, (value * ration_red)), (byte)Math.Min(255, (value * ration_green)), (byte)Math.Min(255, (value * ration_blue)));
            }

            public Color s0 { get { return Color(0); } }

            public Color s1 { get { return Color(32); } }

            public Color s2 { get { return Color(64); } }

            public Color s3 { get { return Color(96); } }

            public Color s4 { get { return Color(128); } }

            public Color s5 { get { return Color(160); } }

            public Color s6 { get { return Color(192); } }

            public Color s7 { get { return Color(224); } }

            public Color s8 { get { return Color(256); } }

        }
    }

    public static class Extentions {
        public static Point Add(this Point p, Point p2) {
            return new Point(p.X + p2.X, p.Y + p2.Y);
        }
        public static Point Add(this Point p, Size s) {
            return new Point(p.X + s.Width, p.Y + s.Height);
        }
        public static Point Sub(this Point p, Point p2) {
            return new Point(p.X - p2.X, p.Y - p2.Y);
        }
        public static Point Sub(this Point p, Size s) {
            return new Point(p.X - s.Width, p.Y - s.Height);
        }
        public static Point Dev(this Point p, double dev) {
            return new Point(p.X / dev, p.Y / dev);
        }
        public static Point Mult(this Point p, double mult) {
            return new Point(p.X * mult, p.Y * mult);
        }

        public static Size Add(this Size p, Size p2) {
            return new Size(p.Width + p2.Width, p.Height + p2.Height);
        }
        public static Size Sub(this Size p, Size p2) {
            return new Size(p.Width - p2.Width, p.Height - p2.Height);
        }
        public static Size Dev(this Size p, double dev) {
            return new Size(p.Width / dev, p.Height / dev);
        }
        public static Size Mult(this Size p, double mult) {
            return new Size(p.Width * mult, p.Height * mult);
        }



        public readonly struct Dpi {
            public double DpiX { get; init; }
            public double DpiY { get; init; }
            public static double Default => 96;
            public Dpi(double dpiX, double dpiY) {
                DpiX = dpiX;
                DpiY = dpiY;
            }
            public Dpi(DpiScale dpiScale) {
                DpiX = dpiScale.DpiScaleX * Default;
                DpiY = dpiScale.DpiScaleY * Default;
            }
        }
        public static Point CalibrateToDpiScale(this Point point) {
            Size scaling = GetScaling();
            return new Point(point.X / scaling.Width, point.Y / scaling.Height);
        }
        public static Size GetScaling() {
            Dpi dpi = GetDpi();
            return new Size(dpi.DpiX / Dpi.Default, dpi.DpiY / Dpi.Default);
        }
        public static Dpi GetDpi() {
            if(Application.Current is null ||
                Application.Current.MainWindow is null ||
                !Application.Current.MainWindow.IsVisible) return new Dpi(Dpi.Default, Dpi.Default);

            return new Dpi(VisualTreeHelper.GetDpi(Application.Current.MainWindow));
        }
    }
}
