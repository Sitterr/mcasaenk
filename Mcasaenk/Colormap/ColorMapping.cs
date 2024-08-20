using System.Collections.Frozen;
using static Mcasaenk.Global;
using System.IO;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using Mcasaenk.Resources;
using Mcasaenk.WorldInfo;
using static Mcasaenk.Colormaping.Tint;
using System.Security.Policy;
using System.DirectoryServices.ActiveDirectory;
using System;
using System.Xml.Linq;

namespace Mcasaenk.Colormaping {

    public class Colormap {
        public const int DEFHEIGHT = 64;

        public const ushort INVBLOCK = ushort.MaxValue, NONEBLOCK = ushort.MaxValue - 1, ERRORBLOCK = ushort.MaxValue - 2;

        public ushort BLOCK_AIR = INVBLOCK, BLOCK_WATER = INVBLOCK;

        public readonly ushort depth;

        public readonly BlockRegistry Block;
        public readonly BiomeRegistry Biome;
        private IDictionary<ushort, BlockValue> blocks;
        private List<Tint> tints;

        public Colormap(RawColormap rawmap, int world_version, DatapacksInfo datapacksInfo) {
            ushort PLAINSBIOME = 0;
            Biome = new BiomeRegistry((name, id) => {
                switch(name) {
                    case "minecraft:plains":
                        PLAINSBIOME = id;
                        break;
                }
            });
            Biome.SetUp(datapacksInfo.biomes.Values.ToList());

            Block = new(Global.Settings.SKIP_UNKNOWN_BLOCKS ? INVBLOCK : NONEBLOCK, (name, id) => {
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

            if(rawmap != null) {
                foreach(var t in rawmap.tints) {
                    var sprite = t.image?.ToBitmapSource()?.ToUIntMatrix();
                    Tint tint = NullTint.Tint;
                    var format = TintFormat.GetFormat(t.format);
                    if(format != null) {
                        if(format.tintclass == typeof(OrthodoxVanillaTint)) tint = new OrthodoxVanillaTint(t.name, world_version, sprite, datapacksInfo);
                        else if(format.tintclass == typeof(HardcodedVanillaTint)) tint = new HardcodedVanillaTint(t.name, t.format, world_version, sprite, datapacksInfo);
                        else if(format.tintclass == typeof(GridTint)) tint = new GridTint(t.name, sprite);
                        else if(format.tintclass == typeof(FixedTint)) tint = new FixedTint(t.name, t.color.ToUInt());
                    }

                    tints.Add(tint);

                    foreach(var block in t.blocks) {
                        blocks[Block.GetId(block.minecraftname())] = new BlockValue() { color = 0xFFFFFFFF, tint = tint };
                    }
                }
                foreach(var b in rawmap.blocks) {
                    ushort id = Block.GetId(b.Key);
                    uint color = b.Value.color.ToUInt();

                    if(blocks.TryGetValue(id, out var block)) {
                        block.color = color;
                    } else {
                        blocks[id] = new BlockValue() { color = color, tint = NullTint.Tint };
                    }
                }
            }


            blocks = blocks.ToFrozenDictionary();
            Block.Freeze();
            Biome.Freeze();

            def = blocks[0];
            error = new BlockValue() { color = 0xFFFF0000, tint = NullTint.Tint };

            Block.LoadOldBlocks();
            depth = Block.GetId("minecraft:water"); // todo!
            Global.Settings.DEFBIOME = PLAINSBIOME;
        }

        public List<Tint> GetTints() => tints;

        private BlockValue def, error;
        public BlockValue Value(ushort block) {
            if(block == ERRORBLOCK) return error;
            else return blocks.GetValueOrDefault(block, def);
        }

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






    public class BlockValue {
        public Tint tint;
        public uint color;

        public uint GetColor(ushort biome, short height) {
            return tint.GetTintedColor(color, biome, height);
        }
    }
}
