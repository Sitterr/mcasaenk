using Mcasaenk.Rendering;
using Mcasaenk.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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


        public static RawTint Read(ReadInterface read, string path_properties, string relbase = "") {
            RawTint tint = new RawTint() {
                name = Path.GetFileNameWithoutExtension(path_properties),
                format = "vanilla",
                color = WPFColor.White,
                yOffset = 0,
            };
            tint.blocks = [tint.name.minecraftname()];
            string source = tint.name + ".png";
            if(relbase == "") relbase = Path.GetDirectoryName(path_properties);

            foreach(string _line in read.ReadAllLines(path_properties)) {
                string line = _line.Trim();
                if(line.Length == 0) continue;

                switch(line.Substring(0, line.IndexOf('='))) {
                    case "source":
                        source = line.Substring(line.IndexOf("=") + 1).Trim();
                        break;
                    case "format":
                        tint.format = line.Substring(line.IndexOf("=") + 1).Trim();
                        break;
                    case "blocks":
                        tint.blocks = line.Substring(line.IndexOf("=") + 1).Trim().Split(" ", StringSplitOptions.RemoveEmptyEntries).Select(l => l.minecraftnamecomplex()).ToList();
                        break;
                    case "color":
                        tint.color = WPFColor.FromHex(line.Substring(line.IndexOf("=") + 1).Trim());
                        break;
                    case "yOffset":
                        int.TryParse(line.Substring(line.IndexOf("=") + 1).Trim(), out tint.yOffset);
                        break;
                }
            }

            tint.image = read.ReadBitmap(Global.GetFullPath(source, relbase));
            return tint;
        }
        public static bool Is(ReadInterface read, string path) {
            if(Path.GetExtension(path) == ".tint") return true;
            if(Path.GetExtension(path) != ".properties") return false;

            foreach(string _line in read.ReadAllLines(path)) {
                string line = _line.Trim();
                if(line.Length == 0) continue;

                switch(line.Substring(0, line.IndexOf('='))) {
                    case "format":
                        string format = line.Substring(line.IndexOf("=") + 1).Trim();
                        return TintMeta.GetFormat(format) != null;
                }
            }
            return false;
        }
    }
    public class RawFilter {
        public string name;
        public List<string> blocks;
        public double transparency;

        public static RawFilter Read(ReadInterface read, string path) {
            RawFilter filter = new RawFilter() {
                name = Path.GetFileNameWithoutExtension(path),
            };
            filter.blocks = [filter.name.minecraftname()];

            foreach(string _line in read.ReadAllLines(path)) {
                string line = _line.Trim();
                if(line.Length == 0) continue;

                switch(line.Substring(0, line.IndexOf('='))) {
                    case "blocks":
                        filter.blocks = line.Substring(line.IndexOf("=") + 1).Trim().Split(" ", StringSplitOptions.RemoveEmptyEntries).Select(l => l.minecraftnamecomplex()).ToList();
                        break;
                    case "absorbtion":
                        double.TryParse(line.Substring(line.IndexOf("=") + 1).Trim(), out double absorb);
                        if(absorb > 1 && absorb < 16) filter.transparency = (15 - absorb) / 15;
                        else if(absorb < 1) filter.transparency = 1 - absorb;
                        break;
                    case "transparency":
                        double.TryParse(line.Substring(line.IndexOf("=") + 1).Trim(), out filter.transparency);
                        if(filter.transparency > 1 && filter.transparency < 16) filter.transparency = filter.transparency / 15;
                        break;
                }
            }

            return filter;
        }
        public static bool Is(ReadInterface read, string path) {
            if(Path.GetExtension(path) == ".filter") return true;
            if(Path.GetExtension(path) != ".properties") return false;

            foreach(string _line in read.ReadAllLines(path)) {
                string line = _line.Trim();
                if(line.Length == 0) continue;

                switch(line.Substring(0, line.IndexOf('='))) {
                    case "format":
                        string format = line.Substring(line.IndexOf("=") + 1).Trim();
                        return format != "filter";
                }
            }
            return false;
        }
    }
    public class RawColormap {
        public Dictionary<string, RawBlock> blocks;
        public List<RawTint> tints;
        public List<RawFilter> filters;
        public List<string> no3dshadeblocks;
        public string depth;

        public RawColormap() {
            blocks = new Dictionary<string, RawBlock>();
            tints = new List<RawTint>();
            filters = new List<RawFilter>();
            no3dshadeblocks = Shade3DFilter.Default();
            depth = "minecraft:water";
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
                output.SaveLines(tint.name + ".tint", lines);
            }
            foreach(var filter in colormap.filters) {
                List<string> lines = new List<string>();

                lines.Add($"format=filter");
                if(!(filter.blocks.Count == 1 && filter.blocks[0].minecraftname() == filter.name.minecraftname())) lines.Add($"blocks={string.Join(" ", filter.blocks.Select(bl => bl.simplifyminecraftname()))}");
                lines.Add($"transparecy={filter.transparency}");
            }

            output.SaveLines("__palette__.blocks", colormap.blocks.Select(bl => {
                if(bl.Value.color.A > 0) return $"{bl.Key}={bl.Value.color.ToHex(false, false)}";
                else return $"{bl.Key}=-";
            }));

        }

        public static RawColormap Load(string path) {
            using var read = ReadInterface.GetSuitable(path);
            if(read == null) return null;
            RawColormap colormap = new RawColormap();

            foreach(var file in read.GetFiles("")) {
                if(file.EndsWith("noshade.blocks")) {
                    colormap.no3dshadeblocks.Clear();
                    TxtFormatReader.ReadStandartFormat(read.ReadAllText(file), (group, parts) => {
                        colormap.no3dshadeblocks.Add(parts[0].minecraftname());
                    });
                } else if(file.EndsWith("depth.blocks") || file.EndsWith("depth.block")) {
                    TxtFormatReader.ReadStandartFormat(read.ReadAllText(file), (group, parts) => {
                        colormap.depth = parts[0].minecraftname();
                    });
                } else if(RawTint.Is(read, file)) {
                    colormap.tints.Add(RawTint.Read(read, file));
                } else if(RawFilter.Is(read, file)) {
                    colormap.filters.Add(RawFilter.Read(read, file));
                }
            }
            string blockstext = "";
            if(read.ExistsFile("__colormap__")) blockstext = read.ReadAllText("__colormap__");
            if(read.ExistsFile("__palette__.txt")) blockstext = read.ReadAllText("__palette__.txt");
            if(read.ExistsFile("__palette__.blocks")) blockstext = read.ReadAllText("__palette__.blocks");            
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
