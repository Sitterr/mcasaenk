using CommunityToolkit.HighPerformance;
using Mcasaenk;
using Mcasaenk.Rendering;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Xml.Linq;
using static Mcasaenk.Global;
using static Utils.AssetsUtils;

namespace Utils {
    public static class MapColormapMaker {

        public static void FromJavaMap(string path, string resourcepack, WPFBitmap blockMap) {
            using ZipSave output = new ZipSave(path);

            var map = ReadIngameMap(blockMap, GetVanillaBlockNames(resourcepack));

            uint watercolor = 0xFF000000 | Convert.ToUInt32(map["minecraft:water"], 16);
            watercolor = Global.MultShade(watercolor, 220 / 255d);
            map["minecraft:water"] = watercolor.ToString("x8").Substring(2);

            output.SaveLines("__palette__.txt", map.Select(v => $"{v.Key.simplifyminecraftname()}={v.Value}").ToArray());
        }


        public static void FromBedrockMap(string path, string resourcepack, WPFBitmap blockMap, WPFBitmap[] biomeMaps) {
            using ZipSave output = new ZipSave(path);

            var map = ReadIngameMap(blockMap, GetVanillaBlockNames(resourcepack));

            List<(string tint, string[] blocks)> tints = new();
            List<string> tintedBlocks = new();
            TxtFormatReader.ReadStandartFormat(Resources.tintblocks, (string group, string[] parts) => {
                string[] blocks = parts[2].Split(',').Select(v => v.Trim().minecraftname()).ToArray();
                tintedBlocks.AddRange(blocks);
                if(parts[1] == "vanilla_grass" || parts[1] == "vanilla_foliage" || parts[1] == "vanilla_water") {
                    tints.Add((parts[1].Split("_")[1], blocks));
                }
            });
            var biomes = Resources.javabiomes.Split("\r\n").ToArray();

            int plainsIndex = Array.IndexOf(biomes, "plains");

            if(biomeMaps.Length != Math.Ceiling((double)biomes.Length / 25)) throw new Exception("biomi");


            // https://minecraft.wiki/w/Plains
            WPFColor BEDROCK_PLAINS_GRASS_TINT = WPFColor.FromHex("#91BD59");
            WPFColor BEDROCK_PLAINS_FOLIAGE_TINT = WPFColor.FromHex("#77AB2F");


            Dictionary<string, (string tint, List<WPFColor> colors)> colors = new();
            {
                foreach(var entry in ReadBiomeInGameMap()) {
                    if(colors.ContainsKey(entry.block) == false) {
                        colors[entry.block] = (entry.tint, new List<WPFColor>());
                    }

                    colors[entry.block].colors.Add(entry.color);
                }
            }

            WPFBitmap newWaterColors = new WPFBitmap(biomes.Length, 1);
            WPFBitmap newFoliageColors = new WPFBitmap(biomes.Length, 1), newGrassColors = new WPFBitmap(biomes.Length, 1);

            // asume java and bedrock use the same tint for plains
            {
                var baseColor = Dev(colors["minecraft:grass_block"].colors[plainsIndex], BEDROCK_PLAINS_GRASS_TINT);
                for(int i = 0; i < biomes.Length; i++) {
                    newGrassColors.SetPixel(i, 0, Dev(colors["minecraft:grass_block"].colors[i], baseColor));
                }

                baseColor = Dev(colors["minecraft:oak_leaves"].colors[plainsIndex], BEDROCK_PLAINS_FOLIAGE_TINT);
                for(int i = 0; i < biomes.Length; i++) {
                    newFoliageColors.SetPixel(i, 0, Dev(colors["minecraft:oak_leaves"].colors[i], baseColor));
                }
            }

            foreach(var f in colors) {
                if(f.Value.colors.Distinct().Count() > 1) {
                    WPFColor fColor = default;
                    if(f.Value.tint == "foliage") {
                        fColor = newFoliageColors.GetPixel(0, 0);
                    } else if(f.Value.tint == "grass") {
                        fColor = newGrassColors.GetPixel(0, 0);
                    } else if(f.Value.tint == "water") {
                        for(int i = 0; i < biomes.Length; i++) {
                            newWaterColors.SetPixel(i, 0, f.Value.colors[i]);
                        }
                        continue;
                    }

                    var c = Dev(f.Value.colors[0], fColor);
                    map[f.Key] = $"{c.R:X2}{c.G:X2}{c.B:X2}";
                }
            }

            output.SaveLines("__palette__.txt", map.Select(v => $"{v.Key.simplifyminecraftname()}={v.Value}").ToArray());

            output.SaveLines("grass.properties", ["format=grid", $"blocks={string.Join(" ", tints.First(t => t.tint == "grass").blocks.Select(b => b.simplifyminecraftname()))}", "source=grass.png"]);
            output.SaveImage("grass.png", newGrassColors);

            output.SaveLines("foliage.properties", ["format=grid", $"blocks={string.Join(" ", tints.First(t => t.tint == "foliage").blocks.Select(b => b.simplifyminecraftname()))}", "source=foliage.png"]);
            output.SaveImage("foliage.png", newFoliageColors);

            output.SaveLines("water.properties", ["format=grid", $"blocks={string.Join(" ", tints.First(t => t.tint == "water").blocks.Select(b => b.simplifyminecraftname()))}", "source=water.png"]);
            output.SaveImage("water.png", newWaterColors);


            WPFColor Dev(WPFColor c1, WPFColor c2) => WPFColor.FromRgb((byte)(c1.R / (double)c2.R * 255), (byte)(c1.G / (double)c2.G * 255), (byte)(c1.B / (double)c2.B * 255));
            WPFColor Mult(WPFColor c1, WPFColor c2) => WPFColor.FromRgb((byte)(c1.R * (double)c2.R / 255), (byte)(c1.G * (double)c2.G / 255), (byte)(c1.B * (double)c2.B / 255));

            IEnumerable<(string block, string tint, int biome, WPFColor color)> ReadBiomeInGameMap() {
                int bi = 0;
                for(int i = 0; i < biomeMaps.Length; i++) {
                    var biomemap = biomeMaps[i];

                    for(int cz = 0; cz < 5; cz++) {
                        for(int cx = 0; cx < 5; cx++) {
                            if(bi >= biomes.Length) break;

                            int ti = 0;
                            for(int z = 0; z < 8; z++) {
                                for(int x = 0; x < 8; x++) {
                                    if(ti >= tintedBlocks.Count) break;

                                    if(x % 2 == z % 2) {
                                        var color = biomemap.GetPixel(8 + cx * 24 + x, 8 + cz * 24 + z);

                                        var ttint = tints.FirstOrDefault(d => d.blocks.Contains(tintedBlocks[ti]));
                                        if(ttint != default) yield return (tintedBlocks[ti], tints.First(d => d.blocks.Contains(tintedBlocks[ti])).tint, bi, color);

                                        ti++;
                                    }
                                }
                            }

                            bi++;
                        }
                    }
                }
            }

        }









        static Dictionary<string, string> ReadIngameMap(WPFBitmap bitmap, string[] blocks) {
            Dictionary<string, string> map = new Dictionary<string, string>();

            string airColor = "";
            int i = 0;
            for(int z = 8; z < 128 - 8; z++) {
                for(int x = 8; x < 128 - 8; x++) {
                    if(i >= blocks.Length) break;
                    if(z % 2 == x % 2) {
                        var c = bitmap.GetPixel(x, z);
                        string hexcolor = $"{c.R:X2}{c.G:X2}{c.B:X2}";
                        map[blocks[i]] = hexcolor;

                        if(blocks[i] == "minecraft:air") {
                            airColor = hexcolor;
                        }

                        i++;
                    }
                }
            }
            map = map.Where((a) => a.Value != airColor).ToDictionary();

            return map;
        }

    }

}