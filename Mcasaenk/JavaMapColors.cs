using Accessibility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Mcasaenk {
    public static class JavaMapColors {
        private static MapColor nullcolor = new MapColor(0, 0, 0);
        public static readonly MapColor[] originals = [
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
            new MapColor(14, 0xFFfffcf5),
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
            new MapColor(35, 0xFF700200, 0),
            new MapColor(36, 0xFFd1b1a1, 1128),
            new MapColor(37, 0xFF9f5224, 1128),
            new MapColor(38, 0xFF95576c, 1128),
            new MapColor(39, 0xFF706c8a, 1128),
            new MapColor(40, 0xFFba8524, 1128),
            new MapColor(41, 0xFF677535, 1128),
            new MapColor(42, 0xFFa04d4e, 1128),
            new MapColor(43, 0xFF392923, 1128),
            new MapColor(44, 0xFF876b62, 1128),
            new MapColor(45, 0xFF575c5c, 1128),
            new MapColor(46, 0xFF7a4958, 1128),
            new MapColor(47, 0xFF4c3e5c, 1128),
            new MapColor(48, 0xFF4c3223, 1128),
            new MapColor(49, 0xFF4c522a, 1128),
            new MapColor(50, 0xFF8e3c2e, 1128),
            new MapColor(51, 0xFF251610, 1128),
            new MapColor(52, 0xFFbd3031, 2562),
            new MapColor(53, 0xFF943f61, 2562),
            new MapColor(54, 0xFF5c191d, 2562),
            new MapColor(55, 0xFF167e86, 2562),
            new MapColor(56, 0xFF3a8e8c, 2562),
            new MapColor(57, 0xFF562c3e, 2562),
            new MapColor(58, 0xFF14b485, 2562),
            new MapColor(59, 0xFF646464, 2724),
            new MapColor(60, 0xFFd8af93, 2724),
            new MapColor(61, 0xFF7fa796, 3105),
        ];

        public static (byte id, WPFColor color) Nearest(WPFColor color, int version = int.MaxValue) {
            (byte, WPFColor) nearest = (0, WPFColor.Transparent);

            if(color.A == 0) return nearest;

            int bestscore = int.MaxValue;
            foreach(var mapcolor in originals) {
                if(mapcolor.version > version) continue;

                (byte id, WPFColor color)[] variants = [mapcolor.V180, mapcolor.V220, mapcolor.V255, mapcolor.V135];

                foreach(var variant in variants) {
                    int score = (color.R - variant.color.R) * (color.R - variant.color.R) + (color.G - variant.color.G) * (color.G - variant.color.G) + (color.B - variant.color.B) * (color.B - variant.color.B);
                    if(score < bestscore) {
                        bestscore = score;
                        nearest = variant;
                    }
                }


            }
            return nearest;
        }

        public static MapColor GetById(byte id) {
            return originals.FirstOrDefault(d => d.mapid == id / 4);
        }
    }
    

    public struct MapColor {
        public readonly byte mapid;
        public readonly int version;
        private WPFColor color;

        public MapColor(byte id, uint uintcolor, int version = 0) {
            this.mapid = id;
            this.color = uintcolor.ToColor();
            this.version = version;
        }

        public (byte id, WPFColor color) V180 => ((byte)(mapid * 4 + 0), new WPFColor((byte)(color.R * 180 / 255d), (byte)(color.G * 180 / 255d), (byte)(color.B * 180 / 255d)));
        public (byte id, WPFColor color) V220 => ((byte)(mapid * 4 + 1), new WPFColor((byte)(color.R * 220 / 255d), (byte)(color.G * 220 / 255d), (byte)(color.B * 220 / 255d)));
        public (byte id, WPFColor color) V255 => ((byte)(mapid * 4 + 2), color);
        public (byte id, WPFColor color) V135 => ((byte)(mapid * 4 + 3), new WPFColor((byte)(color.R * 135 / 255d), (byte)(color.G * 135 / 255d), (byte)(color.B * 135 / 255d)));
    }
}
