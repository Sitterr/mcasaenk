using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using System.IO;
using System.Runtime.InteropServices;
using Mcasaenk.Rendering;

namespace Mcasaenk {
    [StructLayout(LayoutKind.Sequential)]
    public struct WPFColor {
        public byte B;
        public byte G;
        public byte R;
        public byte A;

        public WPFColor() { }
        public WPFColor(byte r, byte g, byte b, byte a = 255) { R = r; G = g; B = b; A = a; }

        public static WPFColor FromRgb(byte r, byte g, byte b, byte a = 255) => new WPFColor(r, g, b, a);
        public static WPFColor FromRgb(byte d, byte a = 255) => new WPFColor(d, d, d, a);
        public static WPFColor FromArgb(byte a, byte r, byte g, byte b) => new WPFColor(r, g, b, a);
        public static WPFColor FromColor(WPFColor c, byte a = 255) => new WPFColor(c.R, c.G, c.B, a);

        public static WPFColor FromUInt(uint c) => c.ToColor();

        public static WPFColor FromHex(string hex) {
            try {
                hex = hex.Replace("#", string.Empty);
                if(hex.Length == 6) {
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    return FromRgb(r, g, b);
                } else if(hex.Length == 8) {
                    byte a = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte r = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(4, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(6, 2), 16);
                    return FromArgb(r, g, b, a);
                } else throw new Exception();
            }
            catch {
                return WPFColor.Transparent;
            }
        }


        public string ToHex(bool containAlpha = true, bool hashtag = true) {
            if(containAlpha)
                return (hashtag ? "#" : "") + $"{A:X2}{R:X2}{G:X2}{B:X2}";
            else
                return (hashtag ? "#" : "") + $"{R:X2}{G:X2}{B:X2}";
        }

        public static bool operator ==(WPFColor left, WPFColor right) => left.R == right.R && left.G == right.G && left.B == right.B && left.A == right.A;
        public static bool operator !=(WPFColor left, WPFColor right) => !(left == right);

        public static readonly WPFColor Transparent = new WPFColor(0, 0, 0, 0), WhiteTransparent = new WPFColor(255, 255, 255, 0);
        public static readonly WPFColor White = new WPFColor(255, 255, 255);
    }

    public class WPFBitmap {
        private BitmapSource last;
        private bool changed = false;
        private readonly WPFColor[,] pixels;
        public WPFBitmap(BitmapSource bitmapSource) {
            this.last = bitmapSource;
            this.pixels = bitmapSource.ToRGBMatrix();
        }
        public WPFBitmap(int width, int height) {
            this.pixels = new WPFColor[width, height];
        }
        public static WPFBitmap FromBytes(byte[] imageData) {
            if(imageData == null) return null;

            BitmapImage biImg = new BitmapImage();
            MemoryStream ms = new MemoryStream(imageData);
            biImg.BeginInit();
            biImg.StreamSource = ms;
            biImg.EndInit();

            return new WPFBitmap(biImg);
        }

        public int Width { get => pixels.GetLength(0); }
        public int Height { get => pixels.GetLength(1); }

        public WPFColor GetPixel(int x, int y) => (x >= 0 && x < Width && y >= 0 && y < Height) ? pixels[x, y] : WPFColor.Transparent;
        public void SetPixel(int x, int y, WPFColor color) {
            changed = true;
            pixels[x, y] = color;
        }

        public BitmapSource ToBitmapSource(bool save = false) {
            if(changed == true || last == null || (last?.Dispatcher == null && save) || last?.Dispatcher?.CheckAccess() == false) {
                changed = false;
                last = pixels.FromRGBMatrix();                
            }
            return last;
        }
    }

    public static class GlobalColors {
        public static uint[,] ToUIntMatrix(this BitmapSource bitmap) {
            if(bitmap.Format != PixelFormats.Bgra32) bitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);

            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;

            uint[] result = new uint[width * height];
            bitmap.CopyPixels(result, width * 4, 0);

            return result.D2(width, height);
        }
        public static WPFColor[,] ToRGBMatrix(this BitmapSource bitmap) {
            if(bitmap.Format != PixelFormats.Bgra32) {
                bitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            }

            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;

            // I LOVE WPF !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            byte[] hahahahahahahahahahhDoubleAllocationhahahahahahahha = new byte[width * height * 4];
            bitmap.CopyPixels(hahahahahahahahahahhDoubleAllocationhahahahahahahha, width * 4, 0);

            WPFColor[,] result = new WPFColor[width, height];
            for(int x = 0; x < width; x++) {
                for(int y = 0; y < height; y++) {
                    result[x, y] = new WPFColor(
                        hahahahahahahahahahhDoubleAllocationhahahahahahahha[(y * width + x) * 4 + 2],
                        hahahahahahahahahahhDoubleAllocationhahahahahahahha[(y * width + x) * 4 + 1],
                        hahahahahahahahahahhDoubleAllocationhahahahahahahha[(y * width + x) * 4 + 0],
                        hahahahahahahahahahhDoubleAllocationhahahahahahahha[(y * width + x) * 4 + 3]
                        );
                }
            }

            return result;
        }
        public static BitmapSource FromRGBMatrix(this WPFColor[,] matrix) {
            int width = matrix.GetLength(0), height = matrix.GetLength(1);

            WriteableBitmap bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            bitmap.Lock();

            unsafe {
                IntPtr pBackBuffer = bitmap.BackBuffer;

                for(int y = 0; y < height; y++) {
                    for(int x = 0; x < width; x++) {
                        WPFColor p = matrix[x, y];

                        *((uint*)pBackBuffer + y * width + x) = p.ToUInt();
                    }
                }
            }
            bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            bitmap.Unlock();
            bitmap.Freeze();

            return bitmap;
        }

        public static uint ToUInt(this WPFColor c) {
            return (uint)((c.A << 24) | (c.R << 16) | (c.G << 8) | (c.B));
        }
        public static uint ToGLUInt(this WPFColor c) {
            return (uint)((c.A << 24) | (c.B << 16) | (c.G << 8) | (c.R));
        }

        public static string ToHex(this WPFColor c, bool containAlpha, bool hashtag) {
            string basec = c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
            if(containAlpha) basec = c.A.ToString("X2") + basec;
            if(hashtag) basec = "#" + basec;
            return basec;
        }
        public static WPFColor ToColor(this uint color) {
            byte a = (byte)((color >> 24) & 0xFF);
            byte r = (byte)((color >> 16) & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte b = (byte)(color & 0xFF);
            return WPFColor.FromArgb(a, r, g, b);
        }
        public static uint ToGL(this uint color) {
            byte a = (byte)((color >> 24) & 0xFF);
            byte r = (byte)((color >> 16) & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte b = (byte)(color & 0xFF);
            return (uint)((a << 24) | (b << 16) | (g << 8) | (r));
        }

        public static void ToGL(this uint[] data) {
            for(int i = 0; i < data.Length; i++) {
                data[i] = data[i].ToGL();
            }
        }

        public static Color ToWinColor(this WPFColor c) {
            return Color.FromArgb(c.A, c.R, c.G, c.B);
        }

        public static WPFColor Add(this WPFColor c, byte f) {
            return WPFColor.FromArgb(c.A, (byte)Math.Clamp(c.R + f, 0, 255), (byte)Math.Clamp(c.G + f, 0, 255), (byte)Math.Clamp(c.B + f, 0, 255));
        }
        public static bool CloseTo(this WPFColor c, WPFColor other, double threshold) {
            const double MAX_DISTANCE = 441.67;

            double distance = Math.Sqrt(
                Math.Pow(c.R - other.R, 2) +
                Math.Pow(c.G - other.G, 2) +
                Math.Pow(c.B - other.B, 2)
            );

            return distance < MAX_DISTANCE * threshold;
        }
    }
}
