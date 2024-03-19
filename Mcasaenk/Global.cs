using Mcasaenk.Nbt;
using Microsoft.Extensions.ObjectPool;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using static Mcasaenk.Rendering.GenerateTilePool;
using static Mcasaenk.Shade3d.ShadeConstants;

namespace Mcasaenk {
    public class Global {
        public static Random rand = new Random();

        
        static Global(){
            pows2 = new int[32];
            pows2[0] = 1;
            for(int i = 1; i < pows2.Length; i++) {
                pows2[i] = pows2[i - 1] * 2;
            }
        }

        private static int[] pows2;
        public static int Pow2(int i) { 
            return pows2[i];
        }

        public static void Time(Action func, out long time) {
            var st = Stopwatch.StartNew();

            func();

            st.Stop();
            time = st.ElapsedMilliseconds;
        }

        public static uint ToARGBInt(string hex6) {
            return 0xFF000000 | (uint)Convert.ToInt32(hex6, 16);
        }

        public static uint ToARGBInt(byte r, byte g, byte b, byte a = 255) {
            return (uint)((a << 24) | (r << 16) | (g << 8) | (b));
        }

        public static Color FromArgb(double alpha, Color baseColor) {
            return Color.FromArgb((byte)(alpha * 255), baseColor.R, baseColor.G, baseColor.B);
        }


        public static uint AddShade(uint color, int ar, int ag, int ab) {
            byte a = (byte)((color >> 24) & 0xFF);
            byte r = (byte)((color >> 16) & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte b = (byte)(color & 0xFF);

            byte r2 = (byte)Math.Clamp(r + ar, 0, 255);
            byte g2 = (byte)Math.Clamp(g + ag, 0, 255);
            byte b2 = (byte)Math.Clamp(b + ab, 0, 255);
            return (uint)((a << 24) | (r2 << 16) | (g2 << 8) | b2);
        }
        public static uint MultShade(uint color, double ar, double ag, double ab) {
            ar = Math.Clamp(ar, 0, 1);
            ag = Math.Clamp(ar, 0, 1);
            ab = Math.Clamp(ar, 0, 1);

            byte a = (byte)((color >> 24) & 0xFF);
            byte r = (byte)((color >> 16) & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte b = (byte)(color & 0xFF);

            byte r2 = (byte)(r * ar);
            byte g2 = (byte)(g * ag);
            byte b2 = (byte)(b * ab);
            return (uint)((a << 24) | (r2 << 16) | (g2 << 8) | b2);
        }
        public static uint Blend(uint color, uint other, double ratio) {
            ratio = Math.Clamp(ratio, 0, 1);

            byte aA = (byte)(color >> 24 & 0xFF);
            byte aR = (byte)(color >> 16 & 0xFF);
            byte aG = (byte)(color >> 8 & 0xFF);
            byte aB = (byte)(color & 0xFF);

            double bratio = 1 - ratio;
            byte bA = (byte)(other >> 24 & 0xFF);
            byte bR = (byte)(other >> 16 & 0xFF);
            byte bG = (byte)(other >> 8 & 0xFF);
            byte bB = (byte)(other & 0xFF);

            uint a = (uint)(aA * ratio + bA * bratio);
            uint r = (uint)(aR * ratio + bR * bratio);
            uint g = (uint)(aG * ratio + bG * bratio);
            uint b = (uint)(aB * ratio + bB * bratio);

            return a << 24 | r << 16 | g << 8 | b;
        }
        public static (byte r, byte g, byte b, byte a) GetARGB(uint color) {
            byte aA = (byte)(color >> 24 & 0xFF);
            byte aR = (byte)(color >> 16 & 0xFF);
            byte aG = (byte)(color >> 8 & 0xFF);
            byte aB = (byte)(color & 0xFF);

            return (aR, aG, aB, aA);
        }


        public static class Coord {
            public static int fairDev(int a, int b) {
                int res = (int)a / b;
                if(a < 0) {
                    res--;
                }
                return res;
            }

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
            public static int absMod(int a, int m) {
                int res = a % m;
                if(res < 0) {
                    res = m + res;
                }
                return res;
            }
        }


        public class ArrPointerObjectPool<T> : DefaultObjectPool<Arr2DBox<T>> {
            public ArrPointerObjectPool(int count) : base(new ArrPointerPoolPolicy<T>(count)) { }
            public ArrPointerObjectPool(int count, int maximumRetained) : base(new ArrPointerPoolPolicy<T>(count), maximumRetained) { }
            class ArrPointerPoolPolicy<T> : DefaultPooledObjectPolicy<Arr2DBox<T>> {
                private int count;
                public ArrPointerPoolPolicy(int count) {
                    this.count = count;
                }
                public override Arr2DBox<T> Create() {
                    return new Arr2DBox<T>(count);
                }

                public override bool Return(Arr2DBox<T> obj) {
                    for(int i = 0; i < count; i++) obj[i] = null;
                    return true;
                }
            }
        }
        public class Arr2DBox<T> {
            private T[][] data;
            public Arr2DBox(int count) {
                data = new T[count][];
            }
            public Arr2DBox() { }

            public int Length { get => data.Length; }

            public T[] this[int index] {
                get => data[index];
                set => data[index] = value;
            }

            public static implicit operator T[][](Arr2DBox<T> box) => box.data;
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

        public static Color ToColor(this uint color) {
            byte aA = (byte)(color >> 24 & 0xFF);
            byte aR = (byte)(color >> 16 & 0xFF);
            byte aG = (byte)(color >> 8 & 0xFF);
            byte aB = (byte)(color & 0xFF);

            return Color.FromArgb(aA, aR, aG, aB);
        }
        public static bool ContainsP(this List<(RegionDir dir, Point2i p)> list, Point2i p) {
            foreach(var el in list) {
                if(el.p == p) return true;
            }
            return false;
        }

        public static IList<T> Shuffle<T>(this IEnumerable<T> sequence) {
            return sequence.Shuffle(Global.rand);
        }
        public static IList<T> Shuffle<T>(this IEnumerable<T> sequence, Random randomNumberGenerator) {
            if(sequence == null) {
                throw new ArgumentNullException("sequence");
            }

            if(randomNumberGenerator == null) {
                throw new ArgumentNullException("randomNumberGenerator");
            }

            T swapTemp;
            List<T> values = sequence.ToList();
            int currentlySelecting = values.Count;
            while(currentlySelecting > 1) {
                int selectedElement = randomNumberGenerator.Next(currentlySelecting);
                --currentlySelecting;
                if(currentlySelecting != selectedElement) {
                    swapTemp = values[currentlySelecting];
                    values[currentlySelecting] = values[selectedElement];
                    values[selectedElement] = swapTemp;
                }
            }

            return values;
        }

        public static T[] DeepCopy<T>(this T[] arr) where T : struct {
            T[] arr2 = new T[arr.Length];
            for(int i = 0; i < arr.Length; i++) arr2[i] = arr[i];
            return arr2;
        }

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
