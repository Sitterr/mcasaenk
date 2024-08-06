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
        public static WPFColor FromArgb(byte a, byte r, byte g, byte b) => new WPFColor(r, g, b, a);

        public static WPFColor FromHex(string hex) {
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

        public WPFColor GetPixel(int x, int y) => pixels[x, y];
        public void SetPixel(int x, int y, WPFColor color) {
            changed = true;
            pixels[x, y] = color;
        }

        public BitmapSource ToBitmapSource() {
            if(changed == true || last == null) {
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

            return bitmap;
        }

        public static uint ToUInt(this WPFColor c) {
            return (uint)((c.A << 24) | (c.R << 16) | (c.G << 8) | (c.B));
        }
        public static WPFColor ToColor(this uint color) {
            byte a = (byte)((color >> 24) & 0xFF);
            byte r = (byte)((color >> 16) & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte b = (byte)(color & 0xFF);
            return WPFColor.FromArgb(a, r, g, b);
        }
    }
}
