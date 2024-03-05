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
        public static uint MultShade(uint color, float ar, float ag, float ab) {
            byte a = (byte)((color >> 24) & 0xFF);
            byte r = (byte)((color >> 16) & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte b = (byte)(color & 0xFF);

            byte r2 = (byte)(r * ar);
            byte g2 = (byte)(g * ag);
            byte b2 = (byte)(b * ab);
            return (uint)((a << 24) | (r2 << 16) | (g2 << 8) | b2);
        }
        public static uint Blend(uint color, uint other, float ratio) {
            ratio = Math.Clamp(ratio, 0, 1);

            uint aA = color >> 24 & 0xFF;
            uint aR = color >> 16 & 0xFF;
            uint aG = color >> 8 & 0xFF;
            uint aB = color & 0xFF;

            float bratio = 1 - ratio;
            uint bA = other >> 24 & 0xFF;
            uint bR = other >> 16 & 0xFF;
            uint bG = other >> 8 & 0xFF;
            uint bB = other & 0xFF;

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

        public static int NbtHash(this string str) {
            return Utf16Hash(str.AsSpan());
        }
        public static int NbtHash(this Span<char> utf16bytes) {
            return Utf16Hash(utf16bytes);
        }
        private static int Utf16Hash(ReadOnlySpan<char> utf16bytes) {
            unchecked {
                int hash1 = (5381 << 16) + 5381;
                int hash2 = hash1;

                for(int i = 0; i < utf16bytes.Length; i += 2) {
                    hash1 = ((hash1 << 5) + hash1) ^ utf16bytes[i];
                    if(i == utf16bytes.Length - 1)
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ utf16bytes[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }


        public static IList<T> Shuffle<T>(this IEnumerable<T> sequence) {
            return sequence.Shuffle(new Random());
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









        public static short SwapEndian(this short value) => unchecked((short)SwapEndian(unchecked((ushort)value)));
        public static ushort SwapEndian(this ushort value) {
            return (ushort)((value << 8) | (value >> 8));
        }

        public static int SwapEndian(this int value) => unchecked((int)SwapEndian(unchecked((uint)value)));
        public static uint SwapEndian(this uint value) {
            value = ((value << 8) & 0xFF00FF00) | ((value >> 8) & 0xFF00FF);
            return (value << 16) | (value >> 16);
        }

        public static long SwapEndian(this long value) => unchecked((long)SwapEndian(unchecked((ulong)value)));
        public static ulong SwapEndian(this ulong value) {
            value = ((value << 8) & 0xFF00FF00FF00FF00UL) | ((value >> 8) & 0x00FF00FF00FF00FFUL);
            value = ((value << 16) & 0xFFFF0000FFFF0000UL) | ((value >> 16) & 0x0000FFFF0000FFFFUL);
            return (value << 32) | (value >> 32);
        }


    }
}
