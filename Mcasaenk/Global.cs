using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Mcasaenk {
    public class Global {
        public static Random rand = new Random();


        public static class Coord {
            public static float absDev(float a, int b) {
                a = (float)Math.Floor(a);
                int res = (int)a / b;
                if(a < 0) {
                    res = ((int)(a + 1) / b) - 1;
                }
                return res;
            }
            public static float absMod(float a, int m) {
                float res = a % m;
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
}
