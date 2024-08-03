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
using Mcasaenk.Resources;
using CommunityToolkit.HighPerformance;
using System.CodeDom;
using Mcasaenk.WorldInfo;
using System.Windows.Documents;
using static Mcasaenk.Rendering.Tint;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Mcasaenk.Rendering {
    public class DynamicNameToIdBiMap {
        private static List<string[]> synonyms = new List<string[]>();
        static DynamicNameToIdBiMap(){
            TxtFormatReader.ReadStandartFormat(ResourceMapping.synonymblocks, (_, parts) => {
                synonyms.Add(parts.Select(w => w.minecraftname()).ToArray());
            });
        }

        private IDictionary<string, ushort> nameToId = new Dictionary<string, ushort>();
        private IDictionary<ushort, string> idToName = new Dictionary<ushort, string>();
        private ushort counter;
        private bool frozen;

        protected ushort def;
        private readonly Action<string, ushort> onAdd;
        public DynamicNameToIdBiMap(ushort def, Action<string, ushort> onAdd) {
            this.def = def;
            this.onAdd = onAdd;
        }

        public ushort GetId(string name) {
            if(nameToId.TryGetValue(name, out var id)) return id;
            else if(frozen == false) return assignNew(name);
            else return def;
        }

        public bool ContainsName(string name) => nameToId.ContainsKey(name);

        public string GetName(ushort id) {
            if(idToName.TryGetValue(id, out string name)) return name;
            return "_unknown_";
        }

        private ushort assignNew(string name) {
            foreach(var synGroup in synonyms) {
                if(synGroup.Contains(name)) {
                    foreach(var syn in synGroup) {
                        nameToId.Add(syn, counter);                       
                        onAdd(syn, counter);
                    }
                    idToName.Add(counter, name);
                    return counter++;
                }
            }
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

        public List<string> GetAllNames() => nameToId.Keys.ToList();
    }

    public class BiomeRegistry : DynamicNameToIdBiMap {
        private static IDictionary<int, ushort> oldBiomeIdToId;

        public BiomeRegistry(Action<string, ushort> onAdd) : base(Global.Settings.DEFBIOME, onAdd) { }

        public void UpdateDef() {
            this.def = Global.Settings.DEFBIOME;
        }

        public ushort GetId(int oldid) {
            if(oldBiomeIdToId.TryGetValue(oldid, out var newid)) return newid;
            return def;
        }
        
        public void SetUp(List<BiomeInfo> biomes) {
            foreach(var biome in biomes.OrderBy(b => b.id)) {
                base.GetId(biome.name);
            }

            oldBiomeIdToId = new Dictionary<int, ushort>();
            TxtFormatReader.ReadStandartFormat(Resources.ResourceMapping.oldbiomes, (_, parts) => {
                int id = Convert.ToInt32(parts[0]);
                string name = parts[1];
                if(name.Contains(":") == false) name = "minecraft:" + name;
                oldBiomeIdToId.Add(id, base.GetId(name));
            });
            oldBiomeIdToId = oldBiomeIdToId.ToFrozenDictionary();
        }
    }

    public class BlockRegistry : DynamicNameToIdBiMap {
        private static IDictionary<int, ushort> oldBlockIdToId;
        public BlockRegistry(Action<string, ushort> onAdd) : base(Colormap.INVBLOCK, onAdd) { }

        public ushort GetId(int oldid) {
            if(oldBlockIdToId.ContainsKey(oldid)) return oldBlockIdToId[oldid];
            return Colormap.NONEBLOCK;
        }

        public void LoadOldBlocks() {
            oldBlockIdToId = new Dictionary<int, ushort>();
            TxtFormatReader.ReadStandartFormat(Resources.ResourceMapping.oldblocks, (_, parts) => {
                string name = parts[0].minecraftname();
                if(base.ContainsName(name)) {
                    int oldidpart1 = Convert.ToInt32(parts[1]) << 4;
                    if(parts[2].Length > 0) {
                        foreach(var s in parts[2].Split(',')) {
                            int oldid = oldidpart1 + Convert.ToInt32(s);
                            oldBlockIdToId[oldid] = base.GetId(name);
                        }
                    } else {
                        for(int i = 0; i < 16; i++) {
                            oldBlockIdToId[oldidpart1 + i] = base.GetId(name);
                        }
                    }
                }
            });
            oldBlockIdToId = oldBlockIdToId.ToFrozenDictionary();
        }
    }

    public class Colormap {
        public const int DEFHEIGHT = 64;

        public const ushort INVBLOCK = ushort.MaxValue, NONEBLOCK = ushort.MaxValue - 1;

        public ushort BLOCK_AIR = INVBLOCK, BLOCK_WATER = INVBLOCK;

        public readonly ushort depth;

        public readonly BlockRegistry Block;
        public readonly BiomeRegistry Biome;
        private IDictionary<ushort, BlockValue> blocks;
        private List<Tint> tints;
        
        public Colormap(string path, int world_version, DatapacksInfo datapacksInfo) {
            ushort PLAINSBIOME = 0;
            Biome = new BiomeRegistry((name, id) => {
                switch(name) {
                    case "minecraft:plains":
                        PLAINSBIOME = id;
                        break;
                }
            });
            Biome.SetUp(datapacksInfo.biomes.Values.ToList());

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
            blocks = new Dictionary<ushort, BlockValue> {
                { Block.GetId("minecraft:air"), new BlockValue(){ color = 0, tint = NullTint.Tint } }
            };

            tints = new List<Tint>();
            foreach(var file in Directory.GetFiles(path)) {
                if(Path.GetExtension(file) != ".properties") continue;

                var t = Tint.ReadTint(file);
                Tint tint = t.format switch {
                    "vanilla" => new OrthodoxVanillaTint(t.name, world_version, t.source, datapacksInfo),
                    "vanilla_grass" => new HardcodedVanillaTint(t.name, "grass", world_version, t.source, datapacksInfo),
                    "vanilla_foliage" => new HardcodedVanillaTint(t.name, "foliage", world_version, t.source, datapacksInfo),
                    "vanilla_water" => new HardcodedVanillaTint(t.name, "water", world_version, t.source, datapacksInfo),
                    "grid" => new GridTint(t.name, t.source),
                    "fixed" => new FixedTint(t.name, t.color),
                };
                tints.Add(tint);

                foreach(var block in t.blocks) {
                    blocks[Block.GetId(block.minecraftname())] = new BlockValue() { color = 0xFFFFFFFF, tint = tint};
                }
            }

            string blockstext = "";
            if(File.Exists(Path.Combine(path, "__palette__.txt"))) blockstext = File.ReadAllText(Path.Combine(path, "__palette__.txt"));
            if(File.Exists(Path.Combine(path, "__colormap__"))) blockstext = File.ReadAllText(Path.Combine(path, "__colormap__"));

            TxtFormatReader.ReadStandartFormat(blockstext, (group, parts) => {
                string name = parts[0].minecraftname(); ushort id = Block.GetId(name);
                string strcolor = parts[1];
                if(strcolor.StartsWith('#')) strcolor = strcolor.Substring(1);
                if(strcolor.Length == 6) strcolor = "FF" + strcolor;
                uint color = Convert.ToUInt32(strcolor, 16);


                if(blocks.TryGetValue(id, out var block)) {
                    block.color = color;
                } else {
                    blocks[id] = new BlockValue() { color = color, tint = NullTint.Tint }; 
                }
            }, '=');


            blocks = blocks.ToFrozenDictionary();
            Block.Freeze();
            Biome.Freeze();

            def = blocks[0];

            Block.LoadOldBlocks();
            depth = Block.GetId("minecraft:water"); // todo!
            Global.Settings.DEFBIOME = PLAINSBIOME;
        }

        public List<Tint> GetTints() => tints;

        private BlockValue def;
        public BlockValue Value(ushort block) => blocks.GetValueOrDefault(block, def);

        public static bool IsColormap(string path) {
            //try {
            //    var data = JsonSerializer.Deserialize<JsonColormap>(File.ReadAllText(Path.Combine(path, "colormap.json")), Global.ColormapJsonOptions());
            //}
            //catch {
            //    return false;
            //}

            return true;
        }




        public readonly static ISet<string> INHERENT_WATER_LOGGED;
        static Colormap() {
            INHERENT_WATER_LOGGED = new HashSet<string>();
            foreach(string bl in ResourceMapping.inherent_water_logged.Split(Environment.NewLine)) {
                INHERENT_WATER_LOGGED.Add(bl.minecraftname());
            }

            INHERENT_WATER_LOGGED = INHERENT_WATER_LOGGED.ToFrozenSet();
        }

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
        public DynamicTintSettings() {
            On = true;
            Blend = 7;
        }

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

    public class DynamicVanillaTintSettings : DynamicTintSettings {
        private double tempHeight;
        public double TemperatureVariation {
            get => tempHeight;
            set {
                if(value == tempHeight) return;

                tempHeight = value;
                OnLightChange(nameof(TemperatureVariation));
            }
        }

        public DynamicVanillaTintSettings() : base() {
            TemperatureVariation = 0;
        }
    }






    public interface Tint {
        public string Name();
        public string Kurz();
        public DynamicTintSettings Settings();

        public uint TintColorFor(ushort biome, short height);
        public uint GetTintedColor(uint baseColor, ushort biome, short height) => Global.ColorMult(baseColor, TintColorFor(biome, height));


        public Blending GetBlendMode();
        public enum Blending { none, heightonly, biomeonly, full }


        public static (string name, string format, string[] blocks, string source, uint color) ReadTint(string path_properties, ReadInterface readInterface) {
            string name = Path.GetFileNameWithoutExtension(path_properties);
            string format = "vanilla";
            string[] blocks = [name.minecraftname()];
            string source = name + ".png";
            uint color = 0xFFFFFFFF;

            foreach(string _line in readInterface.ReadAllLines(path_properties)) {
                string line = _line.Trim();
                if(line.Length == 0) continue;

                switch(line.Substring(0, line.IndexOf('='))) {
                    case "source":
                        source = line.Substring(line.IndexOf("=") + 1).Trim();
                        break;
                    case "format":                   
                        format = line.Substring(line.IndexOf("=") + 1).Trim();
                        break;
                    case "blocks":
                        blocks = line.Substring(line.IndexOf("=") + 1).Trim().Split(" ", StringSplitOptions.RemoveEmptyEntries).Select(l => l.minecraftnamecomplex()).ToArray();
                        break;
                    case "color":
                        color = 0xFF000000 | Convert.ToUInt32(line.Substring(line.IndexOf("=") + 1).Trim(), 16);
                        break;
                }
            }

            source = Path.Combine(Path.GetDirectoryName(path_properties), source);
            return (name, format, blocks, source, color);
        }

        public static (string name, string format, string[] blocks, string source, uint color) ReadTint(string path_properties) {
            string name = Path.GetFileNameWithoutExtension(path_properties);
            string format = "vanilla";
            string[] blocks = [name.minecraftname()];
            string source = name + ".png";
            uint color = 0xFFFFFFFF;

            foreach(string _line in File.ReadAllLines(path_properties)) {
                string line = _line.Trim();
                if(line.Length == 0) continue;

                switch(line.Substring(0, line.IndexOf('='))) {
                    case "source":
                        source = line.Substring(line.IndexOf("=") + 1).Trim();
                        break;
                    case "format":
                        format = line.Substring(line.IndexOf("=") + 1).Trim();
                        break;
                    case "blocks":
                        blocks = line.Substring(line.IndexOf("=") + 1).Trim().Split(" ", StringSplitOptions.RemoveEmptyEntries).Select(l => l.minecraftnamecomplex()).ToArray();
                        break;
                    case "color":
                        color = 0xFF000000 | Convert.ToUInt32(line.Substring(line.IndexOf("=") + 1).Trim(), 16);
                        break;
                }
            }

            source = Path.Combine(Path.GetDirectoryName(path_properties), source);
            return (name, format, blocks, source, color);
        }
    }

    public class OrthodoxVanillaTint : Tint {
        private readonly string name;
        private readonly int version;
        private uint[,] sprite;

        private DatapacksInfo datapacksInfo;
        private DynamicVanillaTintSettings settings = new DynamicVanillaTintSettings();

        public OrthodoxVanillaTint(string name, int verion, string source, DatapacksInfo datapacksInfo) {
            this.name = name;
            this.version = verion;
            this.datapacksInfo = datapacksInfo;

            this.sprite = new BitmapImage(new Uri(source, UriKind.RelativeOrAbsolute)).ToUIntMatrix();
            if(sprite.GetLength(0) != 256 && sprite.GetLength(0) != 256) throw new Exception();
        }
        string Tint.Name() => name; string Tint.Kurz() => "vn";
        DynamicTintSettings Tint.Settings() => settings;

        uint Tint.TintColorFor(ushort biome, short height) {
            return datapacksInfo.biomes[biome].GetOrthodox(sprite, height, version, settings.TemperatureVariation);
        }

        Blending Tint.GetBlendMode() {
            if(settings.On == false) return Blending.none;
            if(settings.Blend == 1) return Blending.heightonly;
            if(settings.TemperatureVariation > 0) return Blending.full;
            else return Blending.biomeonly;
        }

    }
    public class HardcodedVanillaTint : Tint {
        private readonly string name, tint;
        private readonly int version;
        private uint[,] sprite;

        private DatapacksInfo datapacksInfo;
        private DynamicVanillaTintSettings settings = new DynamicVanillaTintSettings();

        public HardcodedVanillaTint(string name, string tint, int version, string source, DatapacksInfo datapackInfo) {
            this.name = name;
            this.tint = tint;
            this.version = version;
            this.datapacksInfo = datapackInfo;

            if(File.Exists(source)) {
                this.sprite = new BitmapImage(new Uri(source, UriKind.RelativeOrAbsolute)).ToUIntMatrix();
                if(sprite.GetLength(0) != 256 && sprite.GetLength(0) != 256) throw new Exception();
            }
        }

        string Tint.Name() => name;
        string Tint.Kurz() => name switch {
            "grass" => "vn_g",
            "foliage" => "vn_f",
            "water" => "vn_w",
            };
        DynamicTintSettings Tint.Settings() => settings;

        uint Tint.TintColorFor(ushort biome, short height) {
            return datapacksInfo.biomes[biome].GetVanilla(tint, sprite, sprite, height, version, settings.TemperatureVariation);
        }

        Blending Tint.GetBlendMode() {
            if(settings.On == false) return Blending.none;
            if(settings.Blend == 1) return Blending.heightonly;
            if(settings.TemperatureVariation > 0 && (tint == "grass" || tint == "foliage")) return Blending.full;
            else return Blending.biomeonly;
        }

    }
    public class FixedTint : Tint { // for every block its own tint
        private readonly string name;

        private uint tint, baseColor;
        private bool hasBaseColor;
        public FixedTint(string name, uint tint, uint baseColor) {
            this.name = name;
            this.tint = tint;
            this.baseColor = Global.ColorMult(tint, baseColor);

            this.hasBaseColor = true;
        }
        string Tint.Name() => name; string Tint.Kurz() => "fx";
        DynamicTintSettings Tint.Settings() => null;
        Blending Tint.GetBlendMode() => Blending.none;

        public FixedTint(string name, uint tint) {
            this.name = name;
            this.tint = tint;
            this.hasBaseColor = false;
        }

        uint Tint.TintColorFor(ushort biome, short height) => tint;
        uint Tint.GetTintedColor(uint baseColor, ushort biome, short height) => hasBaseColor ? this.baseColor : Global.ColorMult(baseColor, tint);
    }
    public class GridTint : Tint {
        private readonly string name;
        private uint[,] sprite;

        private bool heightparity;
        private readonly DynamicTintSettings settings = new DynamicTintSettings();
        public GridTint(string name, string source) {
            this.name = name;

            this.sprite = new BitmapImage(new Uri(source, UriKind.RelativeOrAbsolute)).ToUIntMatrix();

            heightparity = true;
            for(int x = 0; x < sprite.GetLength(0); x++) { 
                uint c = sprite[x, 0];
                for(int y = 0; y < sprite.GetLength(1); y++) {
                    if(sprite[x, y] != c) {
                        heightparity = false;
                        break;
                    }
                }
                if(heightparity == false) break;
            }
        }
        string Tint.Name() => name; string Tint.Kurz() => "gr";
        DynamicTintSettings Tint.Settings() => settings;

        uint Tint.TintColorFor(ushort biome, short height) {
            int x = settings.On ? biome : Global.Settings.DEFBIOME;
            if(x >= sprite.GetLength(0) || x < 0) x = Global.Settings.DEFBIOME;
            int y = Math.Clamp(height, 0, sprite.GetLength(1) - 1);
            return sprite[x, y];
        }



        Blending Tint.GetBlendMode() {
            if(this.settings.On == false) return Blending.none;
            else if(sprite.GetLength(0) == 1 || settings.Blend == 1) return Blending.heightonly;
            else if(sprite.GetLength(1) == 1 || heightparity) return Blending.biomeonly;
            else return Blending.full;
        }


    }
    public class NullTint : Tint {
        public static NullTint Tint = new NullTint();
        private NullTint() { }

        string Tint.Name() => ""; string Tint.Kurz() => "";
        DynamicTintSettings Tint.Settings() => null;
        Blending Tint.GetBlendMode() => Blending.none;

        uint Tint.TintColorFor(ushort biome, short height) => 0xFFFFFFFF;

        uint Tint.GetTintedColor(uint baseColor, ushort biome, short height) => baseColor;
    }










    public class BlockValue {
        public Tint tint;
        public uint color;

        public uint GetColor(ushort biome, short height) {
            return tint.GetTintedColor(color, biome, height);
        }
    }
















    public enum ConvertMode { multiply, additive }
    public enum DepthMode { translucient, map }
}
