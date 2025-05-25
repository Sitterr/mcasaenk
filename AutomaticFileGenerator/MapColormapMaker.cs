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

        public static void FromJavaMap(string path, string[] blocks, WPFBitmap blockMap) {
            using SaveInterface output = SaveInterface.GetSuitable(path);

            var map = ReadIngameMap(blockMap, blocks);

            uint watercolor = 0xFF000000 | Convert.ToUInt32(map["minecraft:water"], 16);
            watercolor = Global.MultShade(watercolor, 220 / 255d);
            map["minecraft:water"] = watercolor.ToString("x8").Substring(2);

            map = map.Select(b => {
                if(b.Value == "-") return b;
                else return new KeyValuePair<string, string>(b.Key, JavaMapColors.Nearest(ColorApproximationAlgorithm.LAB_Euclidean, WPFColor.FromHex(b.Value)).color.ToHex(false, false));
                //else return new KeyValuePair<string, string>(b.Key, JavaMapColors.GetById(JavaMapColors.Nearest(WPFColor.FromHex(b.Value)).id).V255.color.ToHex(false, false)); 
            }).ToDictionary();

            output.SaveLines("__palette__.blocks", map.Select(v => $"{v.Key.simplifyminecraftname()}={v.Value}").ToArray());
        }


        public static void FromBedrockMap(string path, string[] blocks, string tintblockstxt, WPFBitmap blockMap, WPFBitmap[] biomeMaps) {
            using SaveInterface output = SaveInterface.GetSuitable(path);

            var map = ReadIngameMap(blockMap, blocks);

            List<(string tint, string[] blocks, int c)> tints = new();
            List<string> tintedBlocks = new();
            TxtFormatReader.ReadStandartFormat(tintblockstxt, (string group, string[] parts) => {
                if(group == "TINTS") {
                    string[] blocks = parts[2].Split(',').Select(v => v.Trim().minecraftname()).ToArray();
                        tints.Add((parts[0], blocks, 0));
                }
            });
            var biomes = Resources.javabiomes.Split("\r\n").ToArray();

            Dictionary<string, (string tint, List<WPFColor> colors)> colors = new();
            {
                foreach(var entry in ReadBiomeInGameMap()) {
                    if(colors.ContainsKey(entry.block) == false) {
                        colors[entry.block] = (entry.tint, new List<WPFColor>());
                    }

                    colors[entry.block].colors.Add(entry.color);
                }
            }

            tintedBlocks = tintedBlocks.Where(b => colors[b].colors.AllAreSame() == false).ToList();
            WPFColor[,] colormaps = new WPFColor[tintedBlocks.Count, biomes.Length];
            for(int i = 0; i < tintedBlocks.Count; i++) {
                for(int bi = 0; bi < biomes.Length; bi++) {
                    colormaps[i, bi] = Dev(colors[tintedBlocks[i]].colors[bi], WPFColor.White);
                }
            }

            int[] cols = new int[tintedBlocks.Count]; cols[0] = -1;
            for(int i = 1; i < tintedBlocks.Count; i++) {           
                bool set = false;
                for(int ui = 0; ui < i; ui++) {
                    if(cols[ui] != -1) continue;

                    int j;
                    for(j = 0; j < biomes.Length; j++) {
                        if(colormaps[i, j].CloseTo(colormaps[ui, j], 0.05) == false) {
                            break;
                        }
                    }
                    if(j == biomes.Length) {
                        cols[i] = ui;
                        set = true;
                        break;
                    }
                }
                if(!set) cols[i] = -1;
            }


            for(int i = 0; i < tintedBlocks.Count; i++) {
                if(cols[i] != -1) continue;

                string block = tintedBlocks[i];
                var grid = new WPFBitmap(biomes.Length, 1);
                for(int bi = 0; bi < biomes.Length; bi++) {
                    grid.SetPixel(bi, 0, colormaps[i, bi]);
                }
                (string tint, string[] blocks, int c) tint = default;
                for(int f = 0; f < tints.Count; f++) {
                    if(tints[f].blocks.Contains(block)) { 
                        tints[f] = (tints[f].tint, tints[f].blocks, tints[f].c + 1);
                        tint = tints[f];
                        break;
                    }
                }

                string name = tint.c > 1 ? $"{tint.tint}{tint.c}" : tint.tint;
                var finalblocks = Enumerable.Range(0, tintedBlocks.Count).Where(c => cols[c] == i).Select(c => tintedBlocks[c]).Append(block);
                foreach(var fb in finalblocks) {
                    map[fb] = "FFFFFF";
                }

                output.SaveLines(name + ".tint", ["format=grid", $"blocks={string.Join(" ", finalblocks.Select(b => b.simplifyminecraftname()))}"]);
                output.SaveImage(name + ".png", grid);
            }

            map = map.Select(b => {
                if(b.Value == "-") return b;
                else return b;
                //else return new KeyValuePair<string, string>(b.Key, JavaMapColors.GetById(JavaMapColors.Nearest(WPFColor.FromHex(b.Value)).id).V255.color.ToHex(false, false));
                //else return new KeyValuePair<string, string>(b.Key, JavaMapColors.Nearest(WPFColor.FromHex(b.Value)).color.ToHex(false, false));
            }).ToDictionary();
            output.SaveLines("__palette__.blocks", map.Select(v => $"{v.Key.simplifyminecraftname()}={v.Value}").ToArray());


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



        static WPFColor Dev(WPFColor c1, WPFColor c2) => WPFColor.FromRgb((byte)(c1.R / (double)c2.R * 255), (byte)(c1.G / (double)c2.G * 255), (byte)(c1.B / (double)c2.B * 255));
        static WPFColor Mult(WPFColor c1, WPFColor c2) => WPFColor.FromRgb((byte)(c1.R * (double)c2.R / 255), (byte)(c1.G * (double)c2.G / 255), (byte)(c1.B * (double)c2.B / 255));





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
            map = map.Select(b => {
                if(b.Value == airColor) return new KeyValuePair<string, string>(b.Key, "-");
                else return b;
            }).ToDictionary();

            return map;
        }

    }

    static class Global_ {
        public static bool AllAreSame(this List<WPFColor> list)  { 
            foreach(var el in list) if(list.First() != el) return false;
            return true;
        }
    }

}