using Mcasaenk.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Mcasaenk.Global;

namespace Mcasaenk.Colormaping {
    public class RawBlock {
        public WPFColor color;
        public CreationDetails details;
    }
    public class RawTint {
        public string name;
        public string format;
        public List<string> blocks;
        public WPFBitmap image;
        public WPFColor color;
        public int yOffset;
    }
    public class RawColormap {
        public Dictionary<string, RawBlock> blocks;
        public List<RawTint> tints;

        public RawColormap() {
            blocks = new Dictionary<string, RawBlock>();
            tints = new List<RawTint>();
        }


        public static void Save(RawColormap colormap, string path) {
            using var output = SaveInterface.GetSuitable(path);
            if(output == null) return;

            foreach(var tint in colormap.tints) {
                List<string> lines = new List<string>();

                lines.Add($"format={tint.format}");
                if(!(tint.format == "fixed" && tint.blocks.Count == 1 && tint.blocks[0].minecraftname() == tint.name.minecraftname())) lines.Add($"blocks={string.Join(" ", tint.blocks.Select(bl => bl.simplifyminecraftname()))}");
                if(tint.image != null) lines.Add($"source={tint.name}.png");
                if(tint.format == "fixed") lines.Add($"color={tint.color.ToHex(false, false)}");
                if(tint.yOffset != 0) lines.Add($"yOffset={tint.yOffset}");

                if(tint.image != null) output.SaveImage(tint.name + ".png", tint.image);
                output.SaveLines(tint.name + ".properties", lines);
            }

            output.SaveLines("__palette__.txt", colormap.blocks.Select(bl => {
                if(bl.Value.color.A > 0) return $"{bl.Key}={bl.Value.color.ToHex(false, false)}";
                else return $"{bl.Key}=-";
            }));

        }

        public static RawColormap Load(string path) {
            using var read = ReadInterface.GetSuitable(path);
            if(read == null) return null;
            RawColormap colormap = new RawColormap();

            foreach(var file in read.GetFiles("")) {
                if(Path.GetExtension(file) == ".properties") {
                    colormap.tints.Add(Tint.ReadTint(read, file));
                }
            }
            string blockstext = "";
            if(read.ExistsFile("__palette__.txt")) blockstext = read.ReadAllText("__palette__.txt");
            if(read.ExistsFile("__colormap__")) blockstext = read.ReadAllText("__colormap__");
            TxtFormatReader.ReadStandartFormat(blockstext, (group, parts) => {
                string name = parts[0].minecraftname();
                WPFColor color = WPFColor.FromHex(parts[1]);

                colormap.blocks.Add(name, new RawBlock() { color = color });
            }, '=');

            foreach(var tint in colormap.tints) {
                foreach(var block in tint.blocks) {
                    if(colormap.blocks.ContainsKey(block) == false) {
                        colormap.blocks.Add(block, new RawBlock() { color = WPFColor.Transparent });
                    }
                }
            }

            return colormap;
        }
    }
}
