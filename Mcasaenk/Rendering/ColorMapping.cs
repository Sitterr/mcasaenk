using SharpNBT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcasaenk.Rendering {
    public static class ColorMapping {

        public static int GetColor(string blockname, int biome) {
            if(blockname == "minecraft:water") return ToBGRAInt(0, 0, 255);
            else return ToBGRAInt(150, 150, 0);
        }

        private static int ToBGRAInt(byte r, byte g, byte b, byte a = 255) {
            return (a << 24) | (r << 16) | (g << 8) | (b);
        }
    }
}
