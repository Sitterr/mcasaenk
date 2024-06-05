using Accessibility;
using Mcasaenk.Rendering.ChunkRenderData;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Formats.Asn1;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Xml.Linq;
using static Mcasaenk.Global;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Media.Media3D;
using System.ComponentModel;

namespace Mcasaenk.Rendering {
    public class DynamicNameToIdBiMap {
        private IDictionary<string, ushort> nameToId = new Dictionary<string, ushort>();
        private IDictionary<ushort, string> idToName = new Dictionary<ushort, string>();
        private ushort counter;
        private bool frozen;

        private readonly Action<string, ushort> onAdd;
        public DynamicNameToIdBiMap(Action<string, ushort> onAdd) { 
            this.onAdd = onAdd;
        }

        public ushort GetId(string name) {
            if(nameToId.TryGetValue(name, out var id)) return id;
            else if(frozen == false) return assignNew(name);
            else return 0;
        }

        public string GetName(ushort id) {
            return idToName[id];
        }

        private ushort assignNew(string name) {
            nameToId.Add(name, counter);
            idToName.Add(counter, name);
            onAdd(name, counter);
            return counter++;
        }

        public void Freeze() {
            frozen = true;
            nameToId = nameToId.ToFrozenDictionary();
            idToName = idToName.ToFrozenDictionary();
        }

        public void Reset() {
            counter = 0;
            frozen = false;
            nameToId = new Dictionary<string, ushort>();
            idToName = new Dictionary<ushort, string>();
        }
    }

    public static class TxtFormatReader {
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
    }

    public class BiomeRegistry {
        private static IDictionary<int, ushort> oldBiomeIdToId;
        private static IDictionary<string, ushort> biomeNameToId;
        public static ushort GetBiomeByOldId(int oldid) {
            if(oldBiomeIdToId.TryGetValue(oldid, out var id)) return id;
            else return oldBiomeIdToId[-1];
        }
        public static ushort GetBiomeByName(string name) {
            if(biomeNameToId.TryGetValue(name, out var id)) return id;
            else return 0;
        }
        public static int GetBiomeCount() => biomeNameToId.Count;

        public static void Initialize() {
            biomeNameToId = new Dictionary<string, ushort>();

            TxtFormatReader.ReadStandartFormat(Resources.ResourceMapping.biomes, (_, parts) => {
                string name = parts[0];
                ushort id = Convert.ToUInt16(parts[1]);
                biomeNameToId.Add(name, id);
            });
            biomeNameToId = biomeNameToId.ToFrozenDictionary();

            oldBiomeIdToId = new Dictionary<int, ushort>();
            TxtFormatReader.ReadStandartFormat(Resources.ResourceMapping.oldbiomes, (_, parts) => {
                int id = Convert.ToInt32(parts[0]);
                string name = parts[1];
                oldBiomeIdToId.Add(id, biomeNameToId[name]);
            });
            oldBiomeIdToId = oldBiomeIdToId.ToFrozenDictionary();
        }
    }

    public class Colormap {
        public const ushort DEFBIOME = 0;
        public const int DEFHEIGHT = 70;

        public readonly Depth depth;

        public static ushort BLOCK_AIR, BLOCK_WATER;
        public readonly DynamicNameToIdBiMap Block;

        private IDictionary<ushort, BlockValue> blocks;
        private List<Tint2> tints;
        public Colormap(string path) {
            var options = new JsonSerializerOptions { IncludeFields = true };
            options.Converters.Add(new Global.HexConverter());
            options.Converters.Add(new JsonStringEnumConverter());
            var data = JsonSerializer.Deserialize<JsonColormap>(File.ReadAllText(Path.Combine(path, "colormap.json")), options);

            Block = new((name, id) => {
                switch(name) {
                    case "minecraft:air":
                        BLOCK_AIR = id;
                        break;
                    case "minecraft:water":
                        BLOCK_WATER = id;
                        break;
                }
            });

            tints = new List<Tint2>();

            blocks = new Dictionary<ushort, BlockValue> {
                { Block.GetId(blockname("minecraft:air")), new FixedBlock(0x00000000) }
            };

            foreach(var bl in data.blocks) {
                var jel = (JsonElement)bl.value;

                BlockValue val = null;
                if(jel.ValueKind == JsonValueKind.String) {
                    val = new FixedBlock(Global.ToARGBInt(jel.Deserialize<string>(options)));
                } else if(jel.ValueKind == JsonValueKind.Object) {
                    var gridvalue = jel.Deserialize<JsonBlock.GridValue>(options);
                    var tint = tints.FirstOrDefault(t => t.name == gridvalue.tint);

                    bool error = false;
                    if(tint == default) {
                        if(File.Exists(Path.Combine(path, gridvalue.tint))) {
                            tint = new Tint2(path, gridvalue.tint);
                            tints.Add(tint);
                        } else {
                            error = true;
                        }
                    }

                    if(error == false) val = new GridBlock(tint, gridvalue.baseColor);
                    else val = new FixedBlock(gridvalue.baseColor);
                }

                blocks.TryAdd(Block.GetId(blockname(bl.id)), val);
            }
            blocks = blocks.ToFrozenDictionary();

            switch(data.depth_mode) {
                case DepthMode.translucient:
                    depth = TranslucientDepth.DEF(Block.GetId(blockname(data.depth_block)));
                    break;
                case DepthMode.map:
                    depth = new MapDepth() { block = Block.GetId(blockname(data.depth_block)) };
                    break;
            }

            Block.Freeze();

            string blockname(string name) {
                if(name.Contains(":") == false) name = "minecraft:" + name;
                return name;
            }
        }

        public List<Tint2> GetTints() => tints;
        public bool HasActiveTints() => tints.Any(t => t.settings.On && t.settings.Blend > 1);

        public BlockValue Value(ushort block) => blocks[block];

        public JsonColormap ToJson() {
            return new JsonColormap() {
                blocks = this.blocks.Select(b => new JsonBlock() { id = Block.GetName(b.Key), value = b.Value.ToJson() }).ToArray(),
                depth_block = Block.GetName(depth.block),
                depth_mode = depth.GetMode(),
            };
        }



        public static bool IsColormap(string path) {
            try {
                var options = new JsonSerializerOptions { IncludeFields = true };
                options.Converters.Add(new Global.HexConverter());
                options.Converters.Add(new JsonStringEnumConverter());
                var data = JsonSerializer.Deserialize<JsonColormap>(File.ReadAllText(Path.Combine(path, "colormap.json")), options);
            }
            catch {
                return false;
            }

            return true;
        }
    }

    public class Depth {
        public ushort block;

        public DepthMode GetMode() {
            if(this is TranslucientDepth) return DepthMode.translucient;
            if(this is MapDepth) return DepthMode.map;
            return default;
        }




        protected bool frozen = true;
        protected Action onLightChange;
        public void SetActions(Action onLightChange) {
            this.onLightChange = onLightChange;
            frozen = false;
        }
    }
    public class TranslucientDepth : Depth, INotifyPropertyChanged {

        private double transparency;
        public double Transparency {
            get => transparency;
            set {
                if(value == transparency) return;

                transparency = value;
                OnLightChange(nameof(Transparency));
            }
        }

        private bool smartshade;
        public bool SmartShade {
            get => smartshade;
            set {
                if(value == smartshade) return;

                smartshade = value;
                OnLightChange(nameof(SmartShade));
            }
        }



        public void OnLightChange(string propertyName) {
            if(frozen == false) onLightChange();
            if(propertyName != "") OnPropertyChanged(propertyName);
        }
        public TranslucientDepth() { }
        public static TranslucientDepth DEF(ushort block) => new TranslucientDepth() {
            block = block,
            Transparency = 0.5,
            SmartShade = true,
        };
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class MapDepth : Depth {
    }

    public class DynamicTintSettings : INotifyPropertyChanged {

        private bool on;
        public bool On {
            get => on;
            set {
                if(value == on) return;

                on = value;
                OnLightChange(nameof(On));
            }
        }
        private int blend;
        public int Blend {
            get => blend;
            set {
                if(value == blend) return;

                blend = value;
                OnLightChange(nameof(Blend));
            }
        }



        bool frozen = true;
        public void OnLightChange(string propertyName) {
            if(frozen == false) onLightChange();
            if(propertyName != "") OnPropertyChanged(propertyName);
        }
        public DynamicTintSettings() { }
        public static DynamicTintSettings DEF() => new DynamicTintSettings() {
            On = true,
            Blend = 7,
        };
        private Action onLightChange;
        public void SetActions(Action onLightChange) {
            this.onLightChange = onLightChange;
            frozen = false;
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }




    public class Tint2 {
        public readonly string name;
        private uint[,] sprite;

        public DynamicTintSettings settings;

        private bool biomeonly = false, heightonly = false;
        public Tint2(string path, string name) {
            this.name = name;

            BitmapImage bitmapImage = new BitmapImage(new Uri(Path.Combine(path, name), UriKind.RelativeOrAbsolute));
            var image = new WriteableBitmap(bitmapImage).ToUIntMatrix();

            sprite = new uint[383, BiomeRegistry.GetBiomeCount()];

            if(image.GetLength(0) == 1) { // biome only
                biomeonly = true;
                for(int i = 0; i < image.GetLength(1); i++) {
                    sprite[0, i] = image[0, i];
                }
                for(int j = 0; j < 383; j++) {
                    for(int i = 0; i < sprite.GetLength(1); i++) {
                        sprite[j, i] = sprite[0, i];
                    }
                }
            } else if(image.GetLength(1) == 1) { // height only
                heightonly = true;
                for(int j = 0; j < image.GetLength(0); j++) {
                    sprite[j, 0] = image[j, 0];
                }
                for(int i = 0; i < sprite.GetLength(1); i++) {
                    for(int j = 0; j < 383; j++) {
                        sprite[j, i] = sprite[j, 0];
                    }
                }
            } else {
                for(int j = 0; j < image.GetLength(0); j++) {
                    for(int i = 0; i < image.GetLength(1); i++) {
                        sprite[j, i] = image[j, i];
                    } for(int i = image.GetLength(1); i < sprite.GetLength(1); i++) {
                        sprite[j, i] = image[j, image.GetLength(1) - 1];
                    }
                } for(int j = image.GetLength(0); j < sprite.GetLength(0); j++) {
                    for(int i = 0; i < image.GetLength(1); i++) {
                        sprite[j, i] = image[image.GetLength(0) - 1, i];
                    }
                }

                for(int j = image.GetLength(0); j < sprite.GetLength(1); j++) {
                    for(int i = image.GetLength(1); i < sprite.GetLength(1); i++) {
                        sprite[j, i] = image[image.GetLength(0) - 1, image.GetLength(1) - 1];
                    }
                }
            }
        }

        public uint GridColor(ushort biome, short height) {
            height = (short)(height + 0);
            if(height <= -65) return sprite[-64 + 64, biome];
            if(height > 319) return sprite[319 + 64, biome];
            if(this.settings.On) return sprite[height + 64, biome];
            else return sprite[Colormap.DEFHEIGHT + 64, Colormap.DEFBIOME];
        }

        public uint MergeColors(uint color1, uint color2) {
            return Global.ColorMult(color1, color2);
        }

        public Blending GetBlendMode() {
            if(this.settings.On == false) return Blending.none;
            else if(heightonly || settings.Blend == 1) return Blending.simple;
            else if(biomeonly) return Blending.biomeonly;
            else return Blending.full;
        }

        public enum Blending { none, simple, biomeonly, full }
    }
    public interface BlockValue {
        public uint GetColor(ushort biome, short height);
        public object ToJson();
    }
    public class FixedBlock(uint color) : BlockValue {
        public uint GetColor(ushort biome, short height) => color;
        public object ToJson() => color;
    }
    public class GridBlock : BlockValue {
        public Tint2 tint;
        public uint baseColor;
        public GridBlock(Tint2 tint, uint baseColor) {
            this.tint = tint;
            this.baseColor = baseColor;
        }

        public uint GetColor(ushort biome, short height) {
            return tint.MergeColors(baseColor, tint.GridColor(biome, height));
        }

        public object ToJson() { return new JsonBlock.GridValue() { baseColor = this.baseColor, tint = this.tint.name }; }
    }















    public class JsonColormap {
        public string depth_block;
        public DepthMode depth_mode;
        public JsonBlock[] blocks;
    }
    public class JsonBlock {
        public string id;
        public object value;

        public class GridValue {
            public string tint;
            public uint baseColor;
        }
    }

    public enum ConvertMode { multiply, additive }
    public enum DepthMode { translucient, map }
}
