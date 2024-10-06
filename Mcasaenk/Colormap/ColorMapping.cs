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
using System.Text.Json.Serialization;

namespace Mcasaenk.Colormaping {

    public class Colormap {
        public const int DEFHEIGHT = 64;

        public const ushort INVBLOCK = ushort.MaxValue, NONEBLOCK = ushort.MaxValue - 1, ERRORBLOCK = ushort.MaxValue - 2;

        public ushort BLOCK_AIR = INVBLOCK, BLOCK_WATER = INVBLOCK;

        public readonly ushort depth;
        public readonly BlockValue depthVal;

        public readonly BlockRegistry Block;
        public readonly BiomeRegistry Biome;
        private IDictionary<ushort, BlockValue> blocks;
        private List<Tint> tints;
        private List<Group> groups;

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

            tints = new List<Tint>() { InvTint.Tint, NullTint.Tint };
            groups = new List<Group>() {
                new Group(0, InvTint.Tint, false, true) { ABSORBTION = 0 }, /*inv*/
                new Group(1, NullTint.Tint, false, false) { ABSORBTION = 15 }, /*err*/
                new Group(2, NullTint.Tint, false, false) { ABSORBTION = 15 }, /*water*/
                new Group(3, NullTint.Tint, false, false) { ABSORBTION = 15 }, /*normal*/
            };

            blocks = new Dictionary<ushort, BlockValue> {
                { Block.GetId("minecraft:air"), new BlockValue(){ color = 0, tint = InvTint.Tint, group = groups[0] } }
            };

            if(rawmap != null) {
                foreach(var t in rawmap.tints) {
                    var sprite = t.image?.ToBitmapSource()?.ToUIntMatrix();
                    Tint tint = NullTint.Tint;
                    var format = TintMeta.GetFormat(t.format);
                    if(format != null) {
                        if(format.tintclass == typeof(OrthodoxVanillaTint)) tint = new OrthodoxVanillaTint(t.name, world_version, sprite, datapacksInfo);
                        else if(format.tintclass == typeof(Vanilla_Grass)) tint = new Vanilla_Grass(t.name, world_version, sprite, datapacksInfo);
                        else if(format.tintclass == typeof(Vanilla_Foliage)) tint = new Vanilla_Foliage(t.name, world_version, sprite, datapacksInfo);
                        else if(format.tintclass == typeof(Vanilla_Water)) tint = new Vanilla_Water(t.name, world_version, sprite, datapacksInfo);
                        else if(format.tintclass == typeof(GridTint)) tint = new GridTint(t.name, t.yOffset, sprite);
                        else if(format.tintclass == typeof(FixedTint)) tint = new FixedTint(t.name, t.color.ToUInt());
                    }
                    var group = new Group(groups.Count, tint, true, false);
                    if(format.tintclass == typeof(Vanilla_Foliage) || t.name == "spruce_leaves" || t.name == "birch_leaves") {
                        group.ABSORBTION = 5;
                    }

                    groups.Add(group);
                    tints.Add(tint);

                    foreach(var block in t.blocks) {
                        blocks[Block.GetId(block.minecraftname())] = new BlockValue() { color = 0xFFFFFFFF, tint = tint, group = group };
                    }
                }
                foreach(var b in rawmap.blocks) {               
                    uint color = b.Value.color.ToUInt();
                    if(color == 0) continue; // todo
                    ushort id = Block.GetId(b.Key);

                    if(blocks.TryGetValue(id, out var block)) {
                        block.color = color;
                    } else {
                        blocks[id] = new BlockValue() { color = color, tint = tints[1], group = groups[3] };
                    }
                }
            }


            blocks = blocks.ToFrozenDictionary();
            Block.Freeze();
            Biome.Freeze();

            def = blocks[0];
            error = new BlockValue() { color = 0xFFFF0000, tint = NullTint.Error };

            Block.LoadOldBlocks();
            depth = Block.GetId("minecraft:water"); // todo!
            depthVal = blocks[depth];
            blocks[depth].group = groups[2];
            groups[2].tint = blocks[depth].tint;
            groups[2].ABSORBTION = 15;
            Global.Settings.DEFBIOME = PLAINSBIOME;
        }

        public List<Tint> GetTints() => tints;
        public List<Group> GetGroups() => groups;


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


    public class Group : StandardizedSettings {
        public readonly bool caneditsettings, caneditblocks;
        public Tint tint;
        public int id;

        public Group(int id, Tint tint, bool caneditsettings = true, bool caneditblocks = true) {
            this.id = id;
            this.tint = tint;
            this.caneditsettings = caneditsettings;
            this.caneditblocks = caneditblocks;

            ABSORBTION = 15;
        }

        public override void SetFromBack() {
            if(ABSORBTION != Transparency) ABSORBTION = Transparency;
        }
        public override void Reset() {
            Transparency = ABSORBTION;
        }
        public override bool ChangedBack() =>
                   ABSORBTION != Transparency;









        private int transparency, transparency_back;
        [JsonIgnore]
        public int Transparency {
            get => transparency_back;
            set {
                if(transparency_back == value) return;

                transparency_back = value;
                OnAutoChange(nameof(Transparency));
                if(Global.App.OpenedSave == null) {
                    transparency = value;
                    OnAutoChange(nameof(ABSORBTION));
                }
            }
        }
        public int ABSORBTION { get => transparency; set { transparency = value; Transparency = value; OnHardChange(nameof(ABSORBTION)); } }
    }


    public class DynamicTintSettings : StandardizedSettings {
        public DynamicTintSettings() {
            On = true;
            Blend = 9;
        }

        public override void SetFromBack() {
            
        }
        public override void Reset() {
            
        }
        public override bool ChangedBack() =>
                   false;


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
    }

    public class DynamicVanillaTintSettings : DynamicTintSettings {
        public DynamicVanillaTintSettings() : base() {
            TemperatureVariation = 0;
        }

        private double tempHeight;
        public double TemperatureVariation {
            get => tempHeight;
            set {
                if(value == tempHeight) return;

                tempHeight = value;
                Global.Settings.OnLightChange(nameof(TemperatureVariation));
            }
        }
    }






    public class BlockValue {
        public Tint tint;
        public uint color;
        public Group group;

        public uint GetColor(ushort biome, short height) {
            return tint.GetTintedColor(color, biome, height);
        }
    }
}
