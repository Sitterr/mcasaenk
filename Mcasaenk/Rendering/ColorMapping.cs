using Accessibility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Mcasaenk.Rendering {



    public static class ColorMapping {

        private static Dictionary<int, string> biomeNameMap;
        static ColorMapping() {
            biomeNameMap = new();
            ReadStandartFormat(Resources.ResourceMapping.biome_names, (_, parts) => {
                int id = Convert.ToInt32(parts[0]);
                string name = parts[1];
                biomeNameMap.Add(id, name);
            });
        }

        public static string GetBiomeById(int biomeid) {
            if(!biomeNameMap.TryGetValue(biomeid, out var name)) return biomeNameMap[-1];
            return name;
        }



        public static void ReadStandartFormat(string data, Action<string, string[]> onRead) {
            string[] lines = data.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            string group = "";
            foreach(string _line in lines) {
                var line = _line.Trim();
                if(line.Length == 0) continue;
                if(line.StartsWith("--") && line.EndsWith("--")) {
                    group = line.Substring(2, line.Length - 4);
                    continue;
                }
                string[] parts = line.Split(';').Select(a => a.Trim()).ToArray();
                onRead(group, parts);
            }
        }



        private static IColorMapping _currentColorMapping = null;
        public static IColorMapping Current { get => _currentColorMapping ??= new MeanColorMapping(); }
    }


    public struct BlockInformation {
        public string biome;
        public int height;
    }
    public interface IColorMapping {
        uint GetColor(string block, BlockInformation blockInformation);
    }

    public class MapColorMapping : IColorMapping {
        private Dictionary<string, uint> colorMap;
        public MapColorMapping() {
            colorMap = new();
            ColorMapping.ReadStandartFormat(Resources.ResourceMapping.block_colors_map, (_, parts) => {
                string name = parts[0];
                if(!name.Contains(":")) name = "minecraft:" + name;

                uint color = 0xFF000000 | (uint)Convert.ToInt32(parts[1], 16);

                colorMap.Add(name, color);
            });
        }
        public uint GetColor(string blockname, BlockInformation blockInformation) {
            if(!colorMap.TryGetValue(blockname, out var color)) return 0x00000000;
            return color;
        }
    }

    public class MeanColorMapping : IColorMapping {
        private Dictionary<string, uint> colorMap;
        private HashSet<string> grassBlocks, foliageBlocks, waterBlocks;
        private Dictionary<string, (uint grassTint, uint foliageTint, uint waterTint)> tintMap;

        public MeanColorMapping() {
            colorMap = new();
            grassBlocks = new(); foliageBlocks = new(); waterBlocks = new();
            tintMap = new();

            ColorMapping.ReadStandartFormat(Resources.ResourceMapping.block_colors_mean, (group, parts) => {
                switch(group) {
                    case "BIOMES": {
                        string name = parts[0];
                        uint grassTint = Global.ToARGBInt(parts[1]), foliageTint = Global.ToARGBInt(parts[2]), waterTint = Global.ToARGBInt(parts[3]);
                        tintMap.Add(parts[0], (grassTint, foliageTint, waterTint));
                        break;
                    }
                    case "COLORS": {
                        string name = parts[0];
                        if(!name.Contains(":")) name = "minecraft:" + name;
                        uint color = Global.ToARGBInt(parts[1]);
                        colorMap.Add(name, color);
                        break;
                    }
                    case "GRASS": {
                        string name = parts[0];
                        if(!name.Contains(":")) name = "minecraft:" + name;
                        grassBlocks.Add(name);
                        break;
                    }
                    case "FOLIAGE": {
                        string name = parts[0];
                        if(!name.Contains(":")) name = "minecraft:" + name;
                        foliageBlocks.Add(name);
                        break;
                    }
                    case "WATER": {
                        string name = parts[0];
                        if(!name.Contains(":")) name = "minecraft:" + name;
                        waterBlocks.Add(name);
                        break;
                    }
                }


            });
        }
        public uint GetColor(string blockname, BlockInformation blockInformation) {
            if(!colorMap.TryGetValue(blockname, out var color)) return 0x00000000;

            if(grassBlocks.Contains(blockname)) {
                color = applyTint(color, tintMap[blockInformation.biome].grassTint);
            } else if(foliageBlocks.Contains(blockname)) {
                color = applyTint(color, tintMap[blockInformation.biome].foliageTint);
            } else if(waterBlocks.Contains(blockname)) {
                color = applyTint(color, tintMap[blockInformation.biome].waterTint);
            }
            return color;
        }

        uint applyTint(uint color, uint tint) {
            uint nr = (tint >> 16 & 0xFF) * (color >> 16 & 0xFF) >> 8;
            uint ng = (tint >> 8 & 0xFF) * (color >> 8 & 0xFF) >> 8;
            uint nb = (tint & 0xFF) * (color & 0xFF) >> 8;
            return color & 0xFF000000 | nr << 16 | ng << 8 | nb;
        }
    }


}
