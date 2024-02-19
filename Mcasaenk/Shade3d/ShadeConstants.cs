using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Mcasaenk.Shade3d {
    public class ShadeConstants {
        public const double MINB = 5;
        public static readonly ShadeConstants MAX = new ShadeConstants(135.1, MINB);
        public static ShadeConstants GLB = new ShadeConstants(Settings.ADEG, Settings.BDEG);

        public readonly double Adeg, Bdeg;
        public readonly double A, B;
        public readonly double cosA, sinA;
        public readonly double cotgB;
        public readonly double cosAcotgB, sinAcotgB;
        public readonly int xp, zp;
        public readonly int rX, rZ;

        public readonly List<Point2i> blockReachFF, blockReachFC, blockReachCF, blockReachCC, regionReach;
        public readonly byte blockReachLenMax;

        public ShadeConstants(double A_deg, double B_deg) {
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

            rX = (byte)Math.Ceiling(Math.Abs(cosAcotgB * (319 + 64)) / 512) + 1;
            rZ = (byte)Math.Ceiling(Math.Abs(sinAcotgB * (319 + 64)) / 512) + 1;

            blockReachFF = CreateReach((int)Math.Floor(Math.Abs(cosAcotgB)) + 1, (int)Math.Floor(Math.Abs(sinAcotgB)) + 1);
            blockReachFC = CreateReach((int)Math.Floor(Math.Abs(cosAcotgB)) + 1, (int)Math.Floor(Math.Abs(sinAcotgB)) + 2);
            blockReachCF = CreateReach((int)Math.Floor(Math.Abs(cosAcotgB)) + 2, (int)Math.Floor(Math.Abs(sinAcotgB)) + 1);
            blockReachCC = CreateReach((int)Math.Floor(Math.Abs(cosAcotgB)) + 2, (int)Math.Floor(Math.Abs(sinAcotgB)) + 2);
            blockReachLenMax = 0;
            blockReachLenMax = (byte)Math.Max(blockReachFF.Count, blockReachLenMax);
            blockReachLenMax = (byte)Math.Max(blockReachFC.Count, blockReachLenMax);
            blockReachLenMax = (byte)Math.Max(blockReachCF.Count, blockReachLenMax);
            blockReachLenMax = (byte)Math.Max(blockReachCC.Count, blockReachLenMax);

            regionReach = CreateReach(rX, rZ);
        }

        private List<Point2i> CreateReach(int x, int z) {
            var reach = new List<Point2i>();

            bool[,] possible = new bool[x, z];
            Ugliest_DDA_Ever_Sorry(possible, 0.0);

            //const double a = 0.9;
            //Bresenham(possible, 0.5, 0.5, 0.5 + x - 1, 0.5 + z - 1);
            //Bresenham(possible, a, 0, a + x - 1, z - 1);
            //Bresenham(possible, 0, a, x - 1, a + z - 1);

            Debug.WriteLine(""); Debug.WriteLine(""); Debug.WriteLine("");
            for(int i = 0; i < x; i++) {
                for(int j = 0; j < z; j++) {
                    if(possible[i, j]) {
                        Debug.Write(" $");
                        reach.Add(new Point2i((x - i) * -this.xp, (z - j) * -this.zp));
                    } else Debug.Write(" .");
                }
                Debug.WriteLine("");
            }
            Debug.WriteLine(""); Debug.WriteLine(""); Debug.WriteLine("");

            return reach;
        }

        private static void Ugliest_DDA_Ever_Sorry(bool[,] arr, double treshold, bool f = true) {
            int _x = 1, _y = 0;
            int x0 = 0, y0 = 0, x1 = arr.GetLength(0) - 1, y1 = arr.GetLength(1) - 1;
            if(!f) {
                x1--;
                y1--;
            }
            double dx = Math.Sqrt(1 * 1 + ((double)(y1 - y0) / (x1 - x0)) * ((double)(y1 - y0) / (x1 - x0)));
            double dy = Math.Sqrt(1 * 1 + ((double)(x1 - x0) / (y1 - y0)) * ((double)(x1 - x0) / (y1 - y0)));

            arr[0, 0] = true;
            arr[x1, y1] = true;

            double rx = 0, ry = 0;
            double lx = 0, ly = 0;
            int x = 0, y = 0;

            rx += dx;
            x++;
            ry += dy;
            y++;
            while(true) {
                double r = Math.Min(rx, ry);


                if(x > arr.GetLength(0) - 1 && y > arr.GetLength(1) - 1) break;

                double xx, yy;
                if(rx < ry) {
                    xx = x;
                    yy = Math.Sqrt(r * r - x * x);
                } else {
                    xx = Math.Sqrt(r * r - y * y);
                    yy = y;
                }



                double xt = Math.Round(xx, 4), yt = Math.Round(yy, 4);
                lx = Math.Round(lx, 4); ly = Math.Round(ly, 4);
                lx -= x - 1;
                ly -= y - 1;
                xt -= x - 1;
                yt -= y - 1;
                bool rev = false;
                if((ly == 0 && xt == 1) || (ly == 0 && yt == 1)) {
                    (xt, yt) = (yt, xt);
                    (lx, ly) = (ly, lx);
                    rev = true;
                }

                double area = 0;
                if(lx == 0 && yt == 1) {
                    area = (1 - ly) * xt / 2;
                } else if(lx == 0 && xt == 1) {
                    area = (1 - yt) * 1 + (yt - ly) * 1 / 2;
                }

                if(rev) area = 1 - area;

                if(area >= treshold) {
                    if(inrange(x - 1 + x0 + _x, y - 1 + y0 + _y)) arr[x - 1 + x0 + _x, y - 1 + y0 + _y] = true;
                }
                if((1 - area) > treshold) {
                    if(inrange(x - 1 + x0 - 1 + _x, y - 1 + y0 + 1 + _y)) arr[x - 1 + x0 - 1 + _x, y - 1 + y0 + 1 + _y] = true;
                }
                if(xt == 1 && yt == 1) {
                    if(inrange(x - 1 + x0 + _x, y - 1 + y0 + 1 + _y)) arr[x - 1 + x0 + _x, y - 1 + y0 + 1 + _y] = true;
                }



                lx = xx; ly = yy;


                bool xm = r == rx, ym = r == ry;
                if(xm) {
                    rx += dx;
                    x++;
                }
                if(ym) {
                    ry += dy;
                    y++;
                }
            }

            bool inrange(int x, int y) {
                if(x >= 0 && x < arr.GetLength(0) && y >= 0 && y < arr.GetLength(1)) return true;
                return false;
            }
        }


        private static void Bresenham(bool[,] bremAppr, double x0, double y0, double x1, double y1) {
            double dx = Math.Abs(x1 - x0);
            double dy = Math.Abs(y1 - y0);

            int x = (int)Math.Floor(x0);
            int y = (int)Math.Floor(y0);

            int n = 1;
            int x_inc, y_inc;
            double error;

            if(dx == 0) {
                x_inc = 0;
                error = Double.PositiveInfinity;
            } else if(x1 > x0) {
                x_inc = 1;
                n += (int)Math.Floor(x1) - x;
                error = (Math.Floor(x0) + 1 - x0) * dy;
            } else {
                x_inc = -1;
                n += x - (int)Math.Floor(x1);
                error = (x0 - Math.Floor(x0)) * dy;
            }

            if(dy == 0) {
                y_inc = 0;
                error -= Double.PositiveInfinity;
            } else if(y1 > y0) {
                y_inc = 1;
                n += (int)Math.Floor(y1) - y;
                error -= (Math.Floor(y0) + 1 - y0) * dx;
            } else {
                y_inc = -1;
                n += y - (int)Math.Floor(y1);
                error -= (y0 - Math.Floor(y0)) * dx;
            }

            for(; n > 0; --n) {
                bremAppr[x, y] = true;

                if(error > 0) {
                    y += y_inc;
                    error -= dx;
                } else {
                    x += x_inc;
                    error += dy;
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
