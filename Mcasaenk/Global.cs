using Mcasaenk.Nbt;
using Microsoft.Extensions.ObjectPool;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static Mcasaenk.Rendering.GenerateTilePool;
using static Mcasaenk.Shade3d.ShadeConstants;
using Mcasaenk.UI;
using Mcasaenk.Rendering.ChunkRenderData;
using Mcasaenk.UI.Canvas;

namespace Mcasaenk {
    public class Global {
        public static Random rand = new Random();

        public static App App { get => (App)Application.Current; }
        public static Settings Settings { get => App.Settings; } // i hate wpf

        public static ViewModel ViewModel;

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
        public static (byte a, byte r, byte g, byte b) FromARGBInt(uint color) {
            byte a = (byte)((color >> 24) & 0xFF);
            byte r = (byte)((color >> 16) & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte b = (byte)(color & 0xFF);
            return (a, r, g, b);
        }
        public static Color FromArgb(double alpha, Color baseColor) {
            return Color.FromArgb((byte)(alpha * 255), baseColor.R, baseColor.G, baseColor.B);
        }


        public static uint ColorAdd(uint color, uint other) {
            byte oa = (byte)((other >> 24) & 0xFF);
            byte or = (byte)((other >> 16) & 0xFF);
            byte og = (byte)((other >> 8) & 0xFF);
            byte ob = (byte)(other & 0xFF);

            return AddShade(color, oa, or, og, ob);
        }
        public static uint AddShade(uint color, int ar, int ag, int ab, int aa = 255) {
            byte a = (byte)((color >> 24) & 0xFF);
            byte r = (byte)((color >> 16) & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte b = (byte)(color & 0xFF);

            byte a2 = (byte)Math.Clamp(a + aa, 0, 255);
            byte r2 = (byte)Math.Clamp(r + ar, 0, 255);
            byte g2 = (byte)Math.Clamp(g + ag, 0, 255);
            byte b2 = (byte)Math.Clamp(b + ab, 0, 255);
            return (uint)((a2 << 24) | (r2 << 16) | (g2 << 8) | b2);
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
        public static uint ColorMult(uint color, uint other) {
            uint nr = (other >> 16 & 0xFF) * (color >> 16 & 0xFF) >> 8;
            uint ng = (other >> 8 & 0xFF) * (color >> 8 & 0xFF) >> 8;
            uint nb = (other & 0xFF) * (color & 0xFF) >> 8;
            return color & 0xFF000000 | nr << 16 | ng << 8 | nb;
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



        public class HexConverter : JsonConverter<uint> {
            public override uint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
                string hexString = reader.GetString();
                if(hexString.StartsWith("0x")) {
                    hexString = hexString.Substring(2);
                }
                return Global.ToARGBInt(hexString);
            }

            public override void Write(Utf8JsonWriter writer, uint value, JsonSerializerOptions options) {
                writer.WriteStringValue($"0x{value:X}");
            }
        }
    }

    public static class Extentions {

        public static uint[,] ToUIntMatrix(this WriteableBitmap writeableBitmap) {
            int width = writeableBitmap.PixelWidth;
            int height = writeableBitmap.PixelHeight;

            // Create a 2D array to hold the pixel data
            uint[,] pixelArray = new uint[height, width];

            // Calculate the stride (width of a single row of pixels in bytes)
            int stride = width * (writeableBitmap.Format.BitsPerPixel / 8);

            // Create a byte array to hold the pixel data
            byte[] pixelData = new byte[height * stride];

            // Copy the pixel data into the byte array
            writeableBitmap.CopyPixels(pixelData, stride, 0);

            // Loop through each pixel in the image
            for(int y = 0; y < height; y++) {
                for(int x = 0; x < width; x++) {
                    int index = (y * stride) + (x * 4); // 4 bytes per pixel (BGRA format)

                    // Extract color components from the byte array
                    byte blue = pixelData[index];
                    byte green = pixelData[index + 1];
                    byte red = pixelData[index + 2];
                    byte alpha = pixelData[index + 3];

                    // Convert the color to a uint value
                    uint pixelValue = (uint)((alpha << 24) | (red << 16) | (green << 8) | blue);

                    // Store the uint value in the array
                    pixelArray[y, x] = pixelValue;
                }
            }

            return pixelArray;
        }

        public static BitmapImage ToImage(this byte[] array) {
            using(var ms = new System.IO.MemoryStream(array)) {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad; // here
                image.StreamSource = ms;
                image.EndInit();
                return image;
            }
        }
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
        public static Point Floor(this Point p) {
            return new Point(Math.Floor(p.X), Math.Floor(p.Y));
        }
        public static Size AsSize(this Point p) {
            return new Size(p.X, p.Y);
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
