using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.Http.Headers;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Media.Media3D;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Mcasaenk.Shade3d {
    public class ShadeConstants {
        public const double MINB = 5;
        //public static ShadeConstants GLB = new ShadeConstants(Global.App.Settings.ADEG, Global.App.Settings.BDEG);

        public static ShadeConstants GLB;

        public readonly int Height;

        public readonly double Adeg, Bdeg;
        public readonly double A, B;
        public readonly double cosA, sinA;
        public readonly double cotgB;
        public readonly double cosAcotgB, sinAcotgB;
        public readonly int xp, zp;
        public readonly int rX, rZ;
        public enum RegionDir { n, l, r, c }; 
        public readonly List<(RegionDir dir, Point2i p)> regionReach, blockReach;
        public readonly byte blockReachLenMax;

        public ShadeConstants(double A_deg) {
            Adeg = A_deg;
            A = DegToRad(Adeg);
            cosA = Round(Math.Cos(A));
            sinA = Round(Math.Sin(A));
        }

        public ShadeConstants(int height, double A_deg, double B_deg) {
            Height = height;
            Adeg = A_deg;
            Bdeg = B_deg;
            A = DegToRad(Adeg);
            B = DegToRad(Bdeg);
            cosA = Round(Math.Cos(A));
            sinA = Round(Math.Sin(A));
            cotgB = Round(1 / Math.Tan(B));
            cosAcotgB = cosA * cotgB;
            sinAcotgB = sinA * cotgB;

            xp = nCeil(cosA);
            zp = -nCeil(sinA);

            rX = (byte)Math.Ceiling(Math.Abs(cosAcotgB * Height) / 512) + 1;//!!!
            rZ = (byte)Math.Ceiling(Math.Abs(sinAcotgB * Height) / 512) + 1;//!!!


            var size = new SizeF(Math.Abs((float)cosAcotgB), Math.Abs((float)sinAcotgB));
            blockReach = CreateReach(new PointF(0, 0), size, true);
            blockReachLenMax = (byte)blockReach.Count;

            regionReach = CreateReach(new PointF(0, 0), new SizeF((float)Math.Abs(cosAcotgB * Height) / 512, (float)Math.Abs(sinAcotgB * Height) / 512), false);//!!!
        }
        private List<(RegionDir dir, Point2i p)> CreateReach(PointF a, SizeF size, bool transf, float precision = 0.85f) {
            var list = new List<(RegionDir dir, Point2i p)>();

            Debug.WriteLine(a + " " + size);
            var res = Tiles(a, size, precision);
            for(int i = 0; i < res.lenX; i++) {
                for(int j = 0; j < res.lenY; j++) {
                    Debug.Write(res[i, j] switch { 
                        RegionDir.n => ".",
                        RegionDir.r => "r",
                        RegionDir.l => "l",
                        RegionDir.c => "c",
                        _ => "$",
                    } + " ");
                    if(res[i, j] == RegionDir.n) continue;



                    Point2i p = new Point2i(i, j);
                    if(transf) p = new Point2i((res.lenX - i - 1) * -this.xp, (res.lenY - j - 1) * -this.zp);

                    list.Add((res[i, j], p));
                }
                Debug.WriteLine("");
            }
            Debug.WriteLine(""); Debug.WriteLine(""); Debug.WriteLine("");

            return list;
        }

        TilesResult Tiles(PointF a, SizeF size, float precision) {
            PointF b = a + size;

            void SetLine(PointF start, PointF end, int steps, int[,] arr) {
                float stepX = (end.X - start.X) / steps, stepY = (end.Y - start.Y) / steps;
                var step = new SizeF(stepX, stepY);
                for(int i = 0; i < steps - 1; i++) {
                    start += step;

                    int x = (int)Math.Floor(start.X), y = (int)Math.Floor(start.Y);
                    if(x >= 0 && x < arr.GetLength(0) && y >= 0 && y < arr.GetLength(1)) arr[x, y]++;
                }
            }

            const float f = 0.99f;
            const float af = 1 - f;

            float d = (1 / precision) * (precision - 1) * (precision - 1) + 0.002f;

            int sidesteps = Math.Max((int)(size.Width / d), (int)(size.Height / d));
            var s2 = size + new SizeF(f, f) - new SizeF(af, af);
            int mainsteps = Math.Max((int)(s2.Width / d), (int)(s2.Height / d));


            int rx = (int)Math.Ceiling(b.X) + 1, ry = (int)Math.Ceiling(b.Y) + 1;
            int[,] l = new int[rx, ry], r = new int[rx, ry], c = new int[rx, ry];
            SetLine(a + new SizeF(-af, 1 + af), b + new SizeF(-af, 1 + af), sidesteps, r); // r
            SetLine(a + new SizeF(1 + af, -af), b + new SizeF(1 + af, -af), sidesteps, l); // l
            SetLine(a + new SizeF(af, af), b + new SizeF(f, f), mainsteps, c); // c

            var res = new TilesResult(l, r, c);
            return res;
        }
        struct TilesResult {
            private int[,] l, r, c;

            public TilesResult(int[,] l, int[,] r, int[,] c) {
                this.l = l;
                this.r = r;
                this.c = c;
            }

            public int lenX { get => l.GetLength(0); }
            public int lenY { get => l.GetLength(1); }

            public RegionDir this[int x, int y] {
                get {
                    int ll = l[x, y], rr = r[x, y], cc = c[x, y];

                    if(ll > 0 && rr > 0) return RegionDir.c;
                    if(ll > 0) return RegionDir.l;
                    if(rr > 0) return RegionDir.r;
                    if(cc > 0) return RegionDir.c;
                    return RegionDir.n;
                }
            }
        }

        private static int fl(int val, int min, int max, int p) {
            if(p <= 0) return val;
            else return max - (val - min) - 1;
        }
        public int flowX(int val, int min, int max) => fl(val, min, max, xp);
        public int nflowX(int val, int min, int max) => fl(val, min, max, -xp);
        public int flowZ(int val, int min, int max) => fl(val, min, max, zp);
        public int nflowZ(int val, int min, int max) => fl(val, min, max, -zp);



        private static double Round(double a) => Math.Round(a, 3);
        private static double DegToRad(double angle) => angle / 180 * Math.PI;
        private static int nCeil(double a) {
            bool neg = a < 0;
            a = (int)Math.Ceiling(Math.Abs(a));
            if(neg) a = -a;
            return (int)a;
        }
    }
}
