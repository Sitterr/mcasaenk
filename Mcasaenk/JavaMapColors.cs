using Accessibility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcasaenk {
    public static class JavaMapColors {
        private static MapColor nullcolor = new MapColor(0, 0, 0);
        private static MapColor[] originals = [
            new MapColor(1, 0xFF7fb238),
            new MapColor(2, 0xFFf7e9a3),
            new MapColor(3, 0xFFc7c7c7),
            new MapColor(4, 0xFFff0000),
            new MapColor(5, 0xFFa0a0ff),
            new MapColor(6, 0xFFa7a7a7),
            new MapColor(7, 0xFF007c00),
            new MapColor(8, 0xFFffffff),
            new MapColor(9, 0xFFa4a8b8),
            new MapColor(10, 0xFF976d4d),
            new MapColor(11, 0xFF707070),
            new MapColor(12, 0xFF4040ff),
            new MapColor(13, 0xFF8f7748),
            new MapColor(14, 0xFFfffdf5),
            new MapColor(15, 0xFFd87f33),
            new MapColor(16, 0xFFb24cd8),
            new MapColor(17, 0xFF6699d8),
            new MapColor(18, 0xFFe5e533),
            new MapColor(19, 0xFF7fcc19),
            new MapColor(20, 0xFFf27fa5),
            new MapColor(21, 0xFF4c4c4c),
            new MapColor(22, 0xFF999999),
            new MapColor(23, 0xFF4c7f99),
            new MapColor(24, 0xFF7f3fb2),
            new MapColor(25, 0xFF334cb2),
            new MapColor(26, 0xFF664c33),
            new MapColor(27, 0xFF667f33),
            new MapColor(28, 0xFF993333),
            new MapColor(29, 0xFF191919),
            new MapColor(30, 0xFFfaee4d),
            new MapColor(31, 0xFF5cdbd5),
            new MapColor(32, 0xFF4a80ff),
            new MapColor(33, 0xFF00d93a),
            new MapColor(34, 0xFF815631),
            new MapColor(35, 0xFF700200),
            new MapColor(36, 0xFFd1b1a1),
            new MapColor(37, 0xFF9f5224),
            new MapColor(38, 0xFF95576c),
            new MapColor(39, 0xFF706c8a),
            new MapColor(40, 0xFFba8524),
            new MapColor(41, 0xFF677535),
            new MapColor(42, 0xFFa04d4e),
            new MapColor(43, 0xFF392923),
            new MapColor(44, 0xFF876b62),
            new MapColor(45, 0xFF575c5c),
            new MapColor(46, 0xFF7a4958),
            new MapColor(47, 0xFF4c3e5c),
            new MapColor(48, 0xFF4c3223),
            new MapColor(49, 0xFF4c522a),
            new MapColor(50, 0xFF8e3c2e),
            new MapColor(51, 0xFF251610),
            new MapColor(52, 0xFFbd3031),
            new MapColor(53, 0xFF943f61),
            new MapColor(54, 0xFF5c191d),
            new MapColor(55, 0xFF167e86),
            new MapColor(56, 0xFF3a8e8c),
            new MapColor(57, 0xFF562c3e),
            new MapColor(58, 0xFF14b485),
            new MapColor(59, 0xFF646464),
            new MapColor(60, 0xFFd8af93),
            new MapColor(61, 0xFF7fa796),
        ];

        public static MapColor[] derivatives = originals.SelectMany(o => 
        new MapColor[] { new MapColor(o, 0), new MapColor(o, 1), new MapColor(o, 2), new MapColor(o, 3) }).ToArray();

        public static MapColor Nearest(uint color, int version) {
            MapColor nearest = nullcolor;
            int bestscore = int.MaxValue;
            foreach(var mapcolor in derivatives) {
                if(mapcolor.version > version) continue;

                var rgbColor = Global.FromARGBInt(color);
                if(rgbColor.a < 255) return nullcolor;

                int score = (rgbColor.r - mapcolor.r) * (rgbColor.r - mapcolor.r) + (rgbColor.g - mapcolor.g) * (rgbColor.g - mapcolor.g) + (rgbColor.b - mapcolor.b) * (rgbColor.b - mapcolor.b);
                if(score < bestscore) { 
                    bestscore = score;
                    nearest = mapcolor;
                }
            }
            return nearest;
        }
    }

    public struct MapColor {
        public readonly byte id;
        public readonly int version;
        public uint color;
        public int r, g, b;

        public MapColor(byte id, uint color, int version = 0) {
            this.id = id;
            this.color = color;
            (_, r, g, b) = Global.FromARGBInt(color);
            this.version = version;
        }

        public MapColor(MapColor mapColor, int type) {
            id = (byte)(mapColor.id * 4 + type);
            double a = type switch { 
                0 => 180 / 255d,
                1 => 220 / 255d,
                2 => 1,
                3 => 135 / 255d,
            };
            color = Global.MultShade(mapColor.color, a);
            (_, r, g, b) = Global.FromARGBInt(color);
            version = mapColor.version;
        }
    }
}
