﻿using Mcasaenk.Rendering;
using Mcasaenk.Resources;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mcasaenk.WorldInfo {
    public class DatapacksInfo {
        public static readonly Dictionary<string, DimensionInfo> vanilladimensions;
        public static readonly Dictionary<string, BiomeInfo> vanillabiomes;
        static DatapacksInfo() {
            vanillabiomes = new Dictionary<string, BiomeInfo>();
            vanilladimensions = new Dictionary<string, DimensionInfo>();
            TxtFormatReader.ReadStandartFormat(ResourceMapping.vanilladatainfo, (group, parts) => {
                if(group == "BIOMES") {
                    var biome = BiomeInfo.FromParts(parts);
                    vanillabiomes[biome.name] = biome;
                } else if(group == "DIMENSIONS") {
                    var dimension = DimensionInfo.FromParts(parts);
                    vanilladimensions[dimension.name] = dimension;
                }
            });
        }




        public readonly ImageSource image;
        public readonly IDictionary<string, DimensionInfo> dimensions;
        public readonly IDictionary<ushort, BiomeInfo> biomes;

        public DatapacksInfo(string path_world) {
            this.dimensions = new Dictionary<string, DimensionInfo>(vanilladimensions);
            var biomes = new Dictionary<string, BiomeInfo>(vanillabiomes);

            if(Path.Exists(Path.Combine(path_world, "datapacks"))) {
                foreach(var file in Directory.GetFiles(Path.Combine(path_world, "datapacks"))) {
                    if(file.EndsWith(".zip")) {
                        var datapack = ZipFile.Open(file, ZipArchiveMode.Read);
                        var entries = datapack.Entries.ToDictionary(entr => entr.FullName);
                        var concatentries = string.Join(Environment.NewLine, datapack.Entries);

                        ImageSource image = null;
                        {
                            if(entries.ContainsKey("pack.png")) {
                                using var str = entries["pack.png"].Open();
                                var decoder = new PngBitmapDecoder(str, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                                image = decoder.Frames[0];
                            }
                        }

                        {
                            var biome_regex = new Regex("data/([^/]+)/worldgen/biome/(.+)\\.json", RegexOptions.Multiline);
                            foreach(Match match in biome_regex.Matches(concatentries)) {
                                string mnamespace = match.Groups[1].Value;
                                string name = match.Groups[2].Value;
                                using var str = entries[match.Value].Open();
                                using var strReader = new StreamReader(str);
                                string content = strReader.ReadToEnd();

                                biomes[mnamespace + ":" + name] = BiomeInfo.FromJson(mnamespace + ":" + name, content) with { image = image };
                            }
                        }

                        {
                            var dimension_regex = new Regex("data/([^/]+)/dimension/([^/]+)\\.json", RegexOptions.Multiline);
                            List<(string dimname, string dimloc)> pre_dimensions = [
                                ("minecraft:overworld", "data/minecraft/dimension_type/overworld.json"),
                                ("minecraft:overworld_caves", "data/minecraft/dimension_type/overworld_caves.json"),
                                ("minecraft:the_nether", "data/minecraft/dimension_type/the_nether.json"),
                                ("minecraft:the_end", "data/minecraft/dimension_type/the_end.json"),
                            ];
                            foreach(Match match in dimension_regex.Matches(concatentries)) {
                                string mnamespace = match.Groups[1].Value;
                                string name = match.Groups[2].Value;

                                using var str = entries[match.Value].Open();
                                using var strReader = new StreamReader(str);
                                string content = strReader.ReadToEnd();

                                var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);
                                if(json.TryGetValue("type", out var el) == false) continue;
                                if(el.ValueKind == JsonValueKind.String) {
                                    var dimname = el.GetString().fromminecraftname();
                                    pre_dimensions.Add((mnamespace + ":" + name, $"data/{dimname.@namespace}/dimension_type/{dimname.name}.json"));
                                } else if(el.ValueKind == JsonValueKind.Object) {
                                    dimensions[mnamespace + ":" + name] = DimensionInfo.FromJson(mnamespace + ":" + name, el.ToString()) with { image = image };
                                }
                            }

                            foreach(var dim in pre_dimensions) {
                                if(entries.ContainsKey(dim.dimloc) == false) continue;

                                using var str = entries[dim.dimloc].Open();
                                using var strReader = new StreamReader(str);
                                string content = strReader.ReadToEnd();
                                dimensions[dim.dimname] = DimensionInfo.FromJson(dim.dimname, content) with { image = image };
                            }
                        }
                    }
                }
            }


            this.biomes = AssingIdsAndRearrange(biomes);
            this.dimensions = this.dimensions.ToFrozenDictionary();
        }


        private static FrozenDictionary<ushort, BiomeInfo> AssingIdsAndRearrange(Dictionary<string, BiomeInfo> biomes) {
            ushort idbr = 0;
            Dictionary<string, ushort> ids = new();
            TxtFormatReader.ReadStandartFormat(ResourceMapping.biomes, (string group, string[] parts) => {
                ids.Add(parts[0].minecraftname(), idbr++);
            });

            var list = biomes.Values.ToArray();
            for(int i = 0; i < list.Length; i++) {
                if(ids.ContainsKey(list[i].name)) {
                    list[i].id = ids[list[i].name];
                }
            }
            var ord = list.Where(l => l.id == ushort.MaxValue).OrderBy(a => a.name).ToArray();
            for(int i = 0; i < ord.Length; i++) {
                ord[i].id = idbr++;
            }

            return list.ToFrozenDictionary(e => e.id);
        }
    }


    public record BiomeInfo {
        public bool fromparts;

        public string name;
        public ushort id = ushort.MaxValue;
        public uint grass_hardcode_color, foliage_hardcode_color, water_color;
        public string grass_color_modifier;
        public double temp, downfall;

        public ImageSource image;

        public uint GetVanilla(string tint, uint[,] grassMap, uint[,] foliageMap, short y, bool heightVariation) {
            if(tint == "water") {
                return water_color;
            } else if(tint == "grass") {
                if(grass_color_modifier == "dark_forest") {
                    return Global.Blend((GetOrthodox(foliageMap, y, heightVariation) & 0xFFFEFEFE), 0xFF28340A, 0.5);
                } else if(grass_color_modifier == "swamp") {
                    return 0xFF4C763C; // todo maybe idk
                } else if(grass_hardcode_color > 0) return grass_hardcode_color;
                else return GetOrthodox(grassMap, y, heightVariation);
            } else if(tint == "foliage") {
                if(foliage_hardcode_color > 0) return foliage_hardcode_color;
                else return GetOrthodox(foliageMap, y, heightVariation);
            }
            return 0;
        }

        public uint GetOrthodox(uint[,] sprite, short absy, bool heightVariation) {
            double adjTemp = Math.Clamp(heightVariation ? GetTemperature(temp, absy) : temp, 0, 1);
            double adjDownfall = Math.Clamp(downfall, 0, 1) * adjTemp;
            return sprite[(int)((1 - adjTemp) * 255), (int)((1 - adjDownfall) * 255)];
        }

        public static double GetTemperature(double deftemp, short absy) {
            int y64 = 64 - Global.Settings.MINY;
            if(absy > y64) {
                return deftemp - (absy - y64) * 0.05F / 30.0F;
            } else {
                return deftemp;
            }
        }




        public string[] ToParts() {
            return [name, Math.Round(temp, 2).ToString(), Math.Round(downfall, 2).ToString(), grass_hardcode_color.ToString(), foliage_hardcode_color.ToString(), water_color.ToString(), grass_color_modifier];
        }

        public static BiomeInfo FromParts(string[] parts) {
            return new BiomeInfo() {
                fromparts = true,

                name = parts[0],
                temp = Convert.ToDouble(parts[1]),
                downfall = Convert.ToDouble(parts[2]),
                grass_hardcode_color = Convert.ToUInt32(parts[3]),
                foliage_hardcode_color = Convert.ToUInt32(parts[4]),
                water_color = Convert.ToUInt32(parts[5]),
                grass_color_modifier = parts[6],
            };
        }

        public static BiomeInfo FromJson(string name, string content) {

            var o = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);

            o["temperature"].TryGetDouble(out double temp);
            o["downfall"].TryGetDouble(out double downfall);

            uint grassCol = 0, folCol = 0, waterCol = 0;
            string grass_color_modifier = "";
            foreach(var el in o["effects"].EnumerateObject()) {
                if(el.Name == "foliage_color") {
                    folCol = 0xFF000000 | el.Value.GetUInt32();
                } else if(el.Name == "grass_color") {
                    grassCol = 0xFF000000 | el.Value.GetUInt32();
                } else if(el.Name == "water_color") {
                    waterCol = 0xFF000000 | el.Value.GetUInt32();
                } else if(el.Name == "grass_color_modifier") {
                    grass_color_modifier = el.Value.GetString();
                }
            }

            temp = Math.Clamp(temp, 0, 1);
            downfall = Math.Clamp(downfall, 0, 1) * temp;

            return new BiomeInfo() {
                name = name.minecraftname(),
                grass_hardcode_color = grassCol,
                foliage_hardcode_color = folCol,
                water_color = waterCol,
                grass_color_modifier = grass_color_modifier,
                temp = temp,
                downfall = downfall
            };
        }
    }
    public record DimensionInfo {
        public bool fromparts;

        public string name;
        public int miny = 0;
        public int height = 256;
        public double ambientLight;
        public string effects;

        public ImageSource image;


        public string[] ToParts() {
            return [name, height.ToString(), miny.ToString(), Math.Round(ambientLight, 2).ToString(), effects];
        }

        public static DimensionInfo FromParts(string[] parts) {
            return new DimensionInfo() {
                fromparts = true,

                name = parts[0],
                height = Convert.ToInt32(parts[1]),
                miny = Convert.ToInt32(parts[2]),
                ambientLight = Convert.ToDouble(parts[3]),
                effects = parts[4],
            };
        }

        public static DimensionInfo FromJson(string name, string content) {
            var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);

            DimensionInfo info = new DimensionInfo() { name = name };

            if(json.TryGetValue("height", out var height)) {
                info.height = height.GetInt32();
            }
            if(json.TryGetValue("min_y", out var min_y)) {
                info.miny = min_y.GetInt32();
            }
            if(json.TryGetValue("ambient_light", out var ambient_light)) {
                info.ambientLight = ambient_light.GetDouble();
            }
            if(json.TryGetValue("effects", out var effects)) {
                info.effects = effects.GetString();
            }

            return info;
        }
    }
}