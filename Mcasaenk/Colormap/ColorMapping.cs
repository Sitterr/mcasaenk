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
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

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
        public readonly GroupManager groupManager;


        public bool Constructing { get; private set; }

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

            groupman = new GroupManager(this);

            tints = new List<Tint>() { InvTint.Tint, NullTint.Tint };
            groupManager = new GroupManager(this);
            groupManager.Groups.Add(new Group(groupManager, InvTint.Tint, true, false, false) { ABSORBTION = 0 });
            groupManager.Groups.Add(new Group(groupManager, NullTint.Tint, true, false, false) { ABSORBTION = 15 });
            groupManager.Groups.Add(new Group(groupManager, NullTint.Error, true, false, false) { ABSORBTION = 15 });
            groupManager.AddBlockToGroup("minecraft:air", groupManager.Groups[0], true);
            //groups.Groups = new List<Group>() {
            //    new Group(groups, InvTint.Tint, false, false, false) { ABSORBTION = 0 }, /*inv*/
            //    new Group(groups, NullTint.Tint, false, false, false) { ABSORBTION = 15 }, /*err*/
            //    new Group(groups, NullTint.Tint, false, false, false) { ABSORBTION = 15 }, /*water*/
            //    new Group(groups, NullTint.Tint, false, false, false) { ABSORBTION = 15 }, /*normal*/
            //};

            blocks = new Dictionary<ushort, BlockValue> {
                { Block.GetId("minecraft:air"), new BlockValue(){ color = 0, tint = InvTint.Tint } }
            };

            groupManager.Groups.Add(new Group(groupManager, NullTint.Tint, true, false, false) { ABSORBTION = 15 });
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
                    var group = new Group(groupManager, tint, true, true, true);
                    if(format.tintclass == typeof(Vanilla_Foliage) || t.name == "spruce_leaves" || t.name == "birch_leaves") {
                        group.ABSORBTION = 10;
                    }

                    groupManager.Groups.Add(group);
                    tints.Add(tint);

                    foreach(var block in t.blocks) {
                        string name = block.minecraftname();
                        groupManager.AddBlockToGroup(name, group, true);
                        blocks[Block.GetId(name)] = new BlockValue() { color = 0xFFFFFFFF, tint = tint };
                    }
                }
                foreach(var b in rawmap.blocks) {               
                    uint color = b.Value.color.ToUInt();
                    if(color == 0) continue; // todo
                    ushort id = Block.GetId(b.Key);

                    if(blocks.TryGetValue(id, out var block)) {
                        block.color = color;
                    } else {
                        groupManager.AddBlockToGroup(b.Key, groupManager.Groups[1], true);
                        blocks[id] = new BlockValue() { color = color, tint = tints[1] };
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

            Group depthgroup = new Group(groupManager, depthVal.tint, false, true, true, true);
            groupManager.Groups.Add(depthgroup);
            groupManager.AddBlockToGroup("minecraft:water", depthgroup);

            foreach(var gr in groupManager.Groups) gr.SetFromBack();
            groupManager.UpdateWhole();
            Global.Settings.DEFBIOME = PLAINSBIOME;

            
        }

        public List<Tint> GetTints() => tints;
        public List<Tint> GetBlendingTints() => tints.Where(t => t.GetBlendMode() == Blending.biomeonly || t.GetBlendMode() == Blending.full).ToList();

        public readonly GroupManager groupman;


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

    public class GroupManager {
        public readonly Colormap colormap;
        public GroupManager(Colormap colormap) {
            this.colormap = colormap;
            this.GROUPS = new List<Group>();
            this.GROUPSINDEX = new Dictionary<Group, int>();
            this.Groups = new List<Group>();
        }

        private readonly List<Group> GROUPS;
        private readonly Dictionary<Group, int> GROUPSINDEX;
        public Group GetGroup(int id) => GROUPS[id];
        public int GetId(Group group) => GROUPSINDEX[group];


        public List<Group> Groups;


        public void UpdateWhole() {
            Groups = Groups.Where(gr => gr.Blocks.Count > 0 || gr.originforthattint).OrderBy(gr => {
                if(gr.tint == InvTint.Tint) return 0;
                if(gr.tint == NullTint.Error) return 1;             
                if(gr.hostdepth) return 2;
                if(gr.tint == NullTint.Tint) return 3;
                return 10;
            }).ToList();

            foreach(var gr in GROUPS) Global.App.SettingsHub.UnlistSettings(gr);
            GROUPS.Clear();
            GROUPSINDEX.Clear();
            for(int i = 0; i < Groups.Count; i++) {
                GROUPS.Add(Groups[i]);
                GROUPSINDEX.Add(GROUPS[i], i);

                foreach(var blockname in Groups[i].BLOCKS) {
                    var block = colormap.Value(colormap.Block.GetId(blockname));
                    block.group = Groups[i];
                }

                Global.App.SettingsHub.RegisterSettings(Groups[i]);
            }
        }


        public void AddBlockToGroup(string block, Group group, bool neww = false) {
            if(neww == false) {
                foreach(var gr in Groups) {
                    if(gr.Blocks.Contains(block)) {                     
                        gr.Blocks.Remove(block);
                        if(gr.SettingsHub == null) gr.BLOCKS.Remove(block);
                        gr.OnAutoChange(nameof(gr.Blocks));
                        break;
                    }
                }
            }
            
            group.Blocks.Add(block);
            if(group.SettingsHub == null) group.BLOCKS.Add(block);
            group.OnAutoChange(nameof(group.Blocks));
        }

        public void RemoveBlockFromGroup(string block, Group group) {
            foreach(var gr in Groups) {
                if(group.tint == gr.tint && gr.originforthattint) {
                    gr.Blocks.Add(block);
                    if(gr.SettingsHub == null) gr.BLOCKS.Add(block);
                    gr.OnAutoChange(nameof(gr.Blocks));
                    break;
                }
            }
            group.Blocks.Remove(block);
            if(group.SettingsHub == null) group.BLOCKS.Remove(block);
            group.OnAutoChange(nameof(group.Blocks));
        }
    }


    public class Group : StandardizedSettings {

        public readonly bool caneditsettings, originforthattint, visible, hostdepth;
        public readonly Tint tint;
        private GroupManager groupManager;

        public Group(GroupManager groupManager, Tint tint, bool originforthattint, bool visible = true, bool caneditsettings = true, bool hostdepth = false) {
            this.groupManager = groupManager;
            this.tint = tint;
            this.originforthattint = originforthattint;
            this.visible = visible;
            this.caneditsettings = caneditsettings;
            this.hostdepth = hostdepth;

            ABSORBTION = 15;

            Blocks = new List<string>();
            BLOCKS = new List<string>();
        }


        public override void SetFromBack() {
            if(ABSORBTION != Absorbtion) ABSORBTION = Absorbtion;

            BLOCKS.Clear();
            BLOCKS.AddRange(Blocks);
            OnHardChange(nameof(BLOCKS));
        }
        public override void Reset() {
            Absorbtion = ABSORBTION;

            Blocks.Clear();
            Blocks.AddRange(BLOCKS);
            OnAutoChange(nameof(Blocks));
        }
        public override bool ChangedBack() =>
                   ABSORBTION != Absorbtion;



        [JsonIgnore]
        public readonly List<string> Blocks;
        public readonly List<string> BLOCKS;

        public int GetId() => groupManager.GetId(this);


        private int absorbtion, absorbtion_back;
        [JsonIgnore]
        public int Absorbtion {
            get => absorbtion_back;
            set {
                if(absorbtion_back == value) return;

                absorbtion_back = value;
                OnAutoChange(nameof(Absorbtion));
                if(Global.App.OpenedSave == null || SettingsHub == null) {
                    absorbtion = value;
                    OnAutoChange(nameof(ABSORBTION));
                }
            }
        }
        public int ABSORBTION { get => absorbtion; set { absorbtion = value; Absorbtion = value; OnHardChange(nameof(ABSORBTION)); } }
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
