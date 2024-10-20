﻿using Mcasaenk.Nbt;
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
using System.Runtime.InteropServices;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media.Media3D;
using static Mcasaenk.Rendering.ColorGausBlur;
using System.Threading.Channels;
using static Mcasaenk.Global;

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

        public static List<string> FromFolder(string path, bool files, bool folders, bool toponly = true) {
            if(Path.Exists(path) == false) return [];

            List<string> res = new List<string>();
            SearchOption searchOption = toponly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;
            if(files) res.AddRange(Directory.GetFiles(path, "", searchOption));
            if(folders) res.AddRange(Directory.GetDirectories(path, "", searchOption));

            return res;
        }
        public static string ReadName(string fileorfolder, bool extention=true) {
            if(fileorfolder == null || fileorfolder == "") return "";
            string filename = extention ? Path.GetFileName(fileorfolder) : Path.GetFileNameWithoutExtension(fileorfolder);
            if(filename == string.Empty) filename = Path.GetDirectoryName(fileorfolder);
            return filename;
        }






        private static int[] pows2;
        public static int Pow2(int i) { 
            return pows2[i];
        }
        public static double Pow(double a, double b) {
            double res = Math.Pow(Math.Abs(a), b);
            if(a < 0 && b < 1) {
                if((1 / b) % 2 == 1) res = -res;
            }
            return res;
        }

        public static void Time(Action func, out long time) {
            var st = Stopwatch.StartNew();

            func();

            st.Stop();
            time = st.ElapsedMilliseconds;
        }

        public static class TxtFormatReader {
            public static void ReadStandartFormat(string data, Action<string, string[]> onRead, char split = ';') {
                string[] lines = data.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                string group = "";
                foreach(string _line in lines) {
                    var line = _line.Trim();
                    if(line.Length == 0) continue;
                    if(line.StartsWith("//")) continue;
                    if(line.StartsWith("--") && line.EndsWith("--")) {
                        group = line.Substring(2, line.Length - 4).Trim();
                        continue;
                    }
                    string[] parts = line.Split(split).Select(a => a.Trim()).ToArray();
                    onRead(group, parts);
                }
            }
        }

        public static Brush CreateCheckerBrush(Color c1, Color c2) {
            DrawingBrush checkerBrush = new DrawingBrush();
            checkerBrush.TileMode = TileMode.Tile;
            checkerBrush.Viewport = new Rect(0, 0, 20, 20); // 20x20 pixel tiles
            checkerBrush.ViewportUnits = BrushMappingMode.Absolute;

            // Create the DrawingGroup for the checker pattern
            DrawingGroup drawingGroup = new DrawingGroup();

            // Background (white)
            GeometryDrawing backgroundDrawing = new GeometryDrawing();
            backgroundDrawing.Brush = new SolidColorBrush(c1);
            backgroundDrawing.Geometry = new RectangleGeometry(new Rect(0, 0, 20, 20));

            // First light gray rectangle (top-left)
            GeometryDrawing grayDrawing1 = new GeometryDrawing();
            grayDrawing1.Brush = new SolidColorBrush(c2);
            grayDrawing1.Geometry = new RectangleGeometry(new Rect(0, 0, 10, 10));

            // Second light gray rectangle (bottom-right)
            GeometryDrawing grayDrawing2 = new GeometryDrawing();
            grayDrawing2.Brush = new SolidColorBrush(c2);
            grayDrawing2.Geometry = new RectangleGeometry(new Rect(10, 10, 10, 10));

            // Add the drawings to the drawing group
            drawingGroup.Children.Add(backgroundDrawing);
            drawingGroup.Children.Add(grayDrawing1);
            drawingGroup.Children.Add(grayDrawing2);

            // Set the drawing group as the drawing of the brush
            checkerBrush.Drawing = drawingGroup;

            return checkerBrush;
        }

        public static ImageSource CreateColorImageSource(Color color, int width, int height) {
            // Step 1: Create a DrawingVisual
            DrawingVisual visual = new DrawingVisual();

            // Step 2: Draw a rectangle with the desired color
            using(DrawingContext context = visual.RenderOpen()) {
                context.DrawRectangle(new SolidColorBrush(color), null, new Rect(0, 0, width, height));
            }

            // Step 3: Create a RenderTargetBitmap and render the visual into it
            RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);

            // Step 4: Return the ImageSource
            return bitmap;
        }

        public static string GetFullPath(string relativePath, string basePath) {
            // Combine the paths
            string combinedPath = Path.Combine(basePath, relativePath);

            // Split the combined path into individual components
            string[] pathSegments = combinedPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Use a stack to handle the segments
            Stack<string> pathStack = new Stack<string>();

            foreach(var segment in pathSegments) {
                if(segment == "..") {
                    if(pathStack.Count > 0) {
                        // Go up one directory level
                        pathStack.Pop();
                    }
                } else if(segment != "." && segment != "") {
                    // Add segment to path stack
                    pathStack.Push(segment);
                }
            }

            // Reconstruct the full path
            string fullPath = string.Join(Path.DirectorySeparatorChar.ToString(), pathStack.Reverse().ToArray());

            return fullPath;
        }

        public static Color FromArgb(double alpha, Color baseColor) {
            return Color.FromArgb((byte)(alpha * 255), baseColor.R, baseColor.G, baseColor.B);
        }
        public static Color ColorAdd(Color baseColor, int add) {
            return Color.FromRgb((byte)(Math.Clamp(baseColor.R + add, 0, 255)), (byte)(Math.Clamp(baseColor.B + add, 0, 255)), (byte)(Math.Clamp(baseColor.G + add, 0, 255)));
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
            ar = Math.Max(ar, 0);
            ag = Math.Max(ar, 0);
            ab = Math.Max(ar, 0);

            byte a = (byte)((color >> 24) & 0xFF);
            byte r = (byte)((color >> 16) & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte b = (byte)(color & 0xFF);

            byte r2 = (byte)(Math.Clamp(r * ar, 0, 255));
            byte g2 = (byte)(Math.Clamp(g * ag, 0, 255));
            byte b2 = (byte)(Math.Clamp(b * ab, 0, 255));
            return (uint)((a << 24) | (r2 << 16) | (g2 << 8) | b2);
        }
        public static uint MultShade(uint color, double a) {
            if(a == 1) return color;
            return MultShade(color, a, a, a);
        }
        public static uint Blend(uint color, uint other, double ratio, bool alphaBlend = false) {
            ratio = Math.Clamp(ratio, 0, 1);

            byte aA = (byte)(color >> 24 & 0xFF);
            if(!alphaBlend) aA = 255;
            byte aR = (byte)(color >> 16 & 0xFF);
            byte aG = (byte)(color >> 8 & 0xFF);
            byte aB = (byte)(color & 0xFF);

            double bratio = 1 - ratio;
            byte bA = (byte)(other >> 24 & 0xFF);
            if(!alphaBlend) bA = 255;
            byte bR = (byte)(other >> 16 & 0xFF);
            byte bG = (byte)(other >> 8 & 0xFF);
            byte bB = (byte)(other & 0xFF);

            if(ratio == 0.5) {
                return (uint)((aA + bA) / 2) << 24 | (uint)((aR + bR) / 2) << 16 | (uint)((aG + bG) / 2) << 8 | (uint)((aB + bB) / 2);
            }

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
        public static WPFColor ColorMult(WPFColor color, WPFColor other) {
            return WPFColor.FromRgb((byte)Math.Clamp(color.R * other.R, byte.MinValue, byte.MaxValue), (byte)Math.Clamp(color.G * other.G, byte.MinValue, byte.MaxValue), (byte)Math.Clamp(color.B * other.B, byte.MinValue, byte.MaxValue));
        }

        public static JsonSerializerOptions ColormapJsonOptions() {
            var options = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
            options.Converters.Add(new Global.HexConverter());
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
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
                return 0xFF000000 | (uint)Convert.ToInt32(hexString, 16);
            }

            public override void Write(Utf8JsonWriter writer, uint value, JsonSerializerOptions options) {
                writer.WriteStringValue($"0x{value:X}");
            }
        }
    }

    public static class Extentions {
        public static JsonElement getObjectOrFirstElement(this JsonElement element, string objectName) {
            if(element.ValueKind == JsonValueKind.Array) {
                return element.EnumerateArray().First().GetProperty(objectName);
            }
            return element.GetProperty(objectName);
        }
        public static string NextString(this Random random, int stringLength) {
            const string allowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789";
            char[] chars = new char[stringLength];

            for(int i = 0; i < stringLength; i++) {
                chars[i] = allowedChars[random.Next(0, allowedChars.Length)];
            }

            return new string(chars);
        }

        public static string minecraftname(this string name) {
            if(name.Contains(":") == false) name = "minecraft:" + name;
            return name;
        }
        public static (string @namespace, string name) fromminecraftname(this string name) {
            name = name.minecraftname();
            string[] parts = name.Split(':');
            return (parts[0], parts[1]);
        }
        public static string simplifyminecraftname(this string name) {
            if(name.StartsWith("minecraft:")) return name.Substring(10);
            return name;
        }
        static Regex tominecraftname_regex = new Regex("^((\\w*:)?\\w+)(:(\\w+=\\w+)(,(\\w+=\\w+))*)?$");
        public static string minecraftnamecomplex(this string name) {
            var match = tominecraftname_regex.Match(name);
            if(match.Success) {
                return match.Groups[1].Value.minecraftname();
            } else return name;
        }

        public static TValue GetValueOrDefault<TKey, TValue>(
    this IDictionary<TKey, TValue> dictionary,
    TKey key,
    TValue defaultValue) {
            return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public static TValue GetValueOrDefault<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary,
            TKey key,
            Func<TValue> defaultValueProvider) {
            return dictionary.TryGetValue(key, out var value) ? value : defaultValueProvider();
        }



        public static T[,] D2<T>(this T[] input, int width, int height) {
            T[,] output = new T[width, height];
            for(int i = 0; i < width; i++) {
                for(int j = 0; j < height; j++) {
                    output[i, j] = input[j * width + i];
                }
            }
            return output;
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
        public static Point Sub(this Point p, int a) {
            return new Point(p.X - a, p.Y - a);
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
        public static Point AsPoint(this Size p) {
            return new Point(p.Width, p.Height);
        }

        public static Point Mid(this Rect r) {
            return r.TopLeft.Add(r.Size.AsPoint().Dev(2));
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
