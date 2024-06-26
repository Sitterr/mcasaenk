using Mcasaenk;
using Mcasaenk.Rendering;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static Mcasaenk.Rendering.JsonBlock;

namespace Utils {
    public static class ColormapMaker {

        public static void FromBedrockMap(string destFolder, string blockMap, string blockPath, string biomePath, string tintblPath, string[] biomeMaps) {
            var map = ReadStatic(blockMap, blockPath);


            var tintedBlocks = File.ReadAllLines(tintblPath).Select(l => (l.Split(';')[0], l.Split(';')[1])).ToArray();
            var biomes = File.ReadAllLines(biomePath).OrderBy((l) => Convert.ToInt16(l.Split(';')[1])).Select(l => l.Split(';')[0]).ToArray();

            if(biomeMaps.Length != Math.Ceiling((double)biomes.Length / 25)) throw new Exception("biomi");

            

            Bitmap grassColors = new Bitmap("C:\\Users\\nikol\\AppData\\Local\\mcasaenk\\colormaps\\mean\\grass.png");
            Bitmap foliageColors = new Bitmap("C:\\Users\\nikol\\AppData\\Local\\mcasaenk\\colormaps\\mean\\foliage.png");



            Dictionary<string, (string tint, List<Color> colors)> colors = new();
            {
                foreach(var entry in ReadBiomes()) {
                    if(colors.ContainsKey(entry.block) == false) {
                        colors[entry.block] = (entry.tint, new List<Color>());
                    }
                    
                    colors[entry.block].colors.Add(entry.color);
                }
            }

            Bitmap newWaterColors = new Bitmap(biomes.Length, 1);
            Bitmap newFoliageColors = new Bitmap(biomes.Length, 1), newGrassColors = new Bitmap(biomes.Length, 1);

            {
                var baseColor = Dev(colors["grass_block"].colors[0], grassColors.GetPixel(0, 0));
                for(int i = 0; i < biomes.Length; i++) {
                    newGrassColors.SetPixel(i, 0, Dev(colors["grass_block"].colors[i], baseColor));
                }

                baseColor = Dev(colors["oak_leaves"].colors[0], foliageColors.GetPixel(0, 0));
                for(int i = 0; i < biomes.Length; i++) {
                    newFoliageColors.SetPixel(i, 0, Dev(colors["oak_leaves"].colors[i], baseColor));
                }
            }

            foreach(var f in colors) {
                if(f.Value.colors.Distinct().Count() > 1) {
                    Color fColor = default;
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

                    var baseColor = unchecked((uint)Dev(f.Value.colors[0], fColor).ToArgb());
                    map[f.Key] = new JsonBlock() { id = f.Key, value = new GridValue() { baseColor = baseColor, tint = f.Value.tint + ".png" } };
                }
            }
            map["water"] = new JsonBlock() { id = "water", value = new GridValue() { baseColor = uint.MaxValue, tint = "water.png" } };


            var colormap = new JsonColormap();
            colormap.depth_block = "water";
            colormap.blocks = map.Values.ToArray();

            Directory.CreateDirectory(destFolder);
            File.WriteAllText(Path.Combine(destFolder, "colormap.json"), JsonSerializer.Serialize<JsonColormap>(colormap, Global.ColormapJsonOptions()));
            newWaterColors.Save(Path.Combine(destFolder, "water.png"));

            newFoliageColors.Save(Path.Combine(destFolder, "foliage.png"));
            newGrassColors.Save(Path.Combine(destFolder, "grass.png"));




            Color Dev(Color c1, Color c2) => Color.FromArgb((int)(c1.R / (double)c2.R * 255), (int)(c1.G / (double)c2.G * 255), (int)(c1.B / (double)c2.B * 255));
            Color Mult(Color c1, Color c2) => Color.FromArgb((int)(c1.R * (double)c2.R / 255), (int)(c1.G * (double)c2.G / 255), (int)(c1.B * (double)c2.B / 255));

            IEnumerable<(string block, string tint, int biome, Color color)> ReadBiomes() {
                int bi = 0;
                for(int i = 0; i < biomeMaps.Length; i++) {
                    var biomemap = new Bitmap(biomeMaps[i]);

                    for(int cz = 0; cz < 5; cz++) {
                        for(int cx = 0; cx < 5; cx++) {
                            if(bi >= biomes.Length) break;

                            int ti = 0;
                            for(int z = 0; z < 8; z++) {
                                for(int x = 0; x < 8; x++) {
                                    if(ti >= tintedBlocks.Length) break;

                                    if(x % 2 == z % 2) {
                                        var color = biomemap.GetPixel(8 + cx * 24 + x, 8 + cz * 24 + z);

                                        yield return (tintedBlocks[ti].Item1, tintedBlocks[ti].Item2, bi, color);

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

        public static void FromJavaMap(string destFolder, string blockMap, string blockPath) {
            var map = ReadStatic(blockMap, blockPath);

            var colormap = new JsonColormap();
            colormap.depth_block = "water";
            colormap.blocks = map.Values.ToArray();

            Directory.CreateDirectory(destFolder);
            File.WriteAllText(Path.Combine(destFolder, "colormap.json"), JsonSerializer.Serialize<JsonColormap>(colormap, Global.ColormapJsonOptions()));
        }









        private static Dictionary<string, JsonBlock> ReadStatic(string blockMap, string blockPath) {
            var bitmap = new Bitmap(blockMap);
            var blocks = File.ReadAllLines(blockPath);

            Dictionary<string, JsonBlock> map = new Dictionary<string, JsonBlock>();

            string airColor = "";
            int i = 0;
            for(int z = 8; z < 128 - 8; z++) {
                for(int x = 8; x < 128 - 8; x++) {
                    if(i >= blocks.Length) break;
                    if(z % 2 == x % 2) {
                        string stcolor = bitmap.GetPixel(x, z).ToArgb().ToString("x").Substring(2);
                        var block = new JsonBlock() { id = blocks[i++], value = stcolor };
                        map[block.id] = block;

                        if(block.id == "air") {
                            airColor = stcolor;
                        }
                    }
                }
            }
            map = map.Where((a) => {
                if(a.Value.value is string s) {
                    return s != airColor;
                } else return true;
            }).ToDictionary();

            return map;
        }
    }
}
