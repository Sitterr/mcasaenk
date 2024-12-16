using System.Collections.Frozen;
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
using System.Windows.Media;
using System.Drawing;
using System.Windows.Documents;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Mcasaenk.Rendering;

namespace Mcasaenk.Colormaping {

    public class Colormap {
        public const int DEFHEIGHT = 64;

        public const ushort INVBLOCK = ushort.MaxValue, NONEBLOCK = ushort.MaxValue - 1, ERRORBLOCK = ushort.MaxValue - 2;
        public ushort BLOCK_AIR = INVBLOCK, BLOCK_WATER = INVBLOCK;
        public readonly ushort depth;
        public readonly ISet<ushort> noShades;

        public readonly BlockRegistry Block;
        public readonly BiomeRegistry Biome;

        private IDictionary<ushort, uint> BlocksManager;   
        public readonly TintManager TintManager;
        public readonly FilterManager FilterManager;
        public readonly TintFilterShadeGrouping Grouping;

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

            
            BlocksManager = new Dictionary<ushort, uint>();
            TintManager = new TintManager(this);
            FilterManager = new FilterManager(this);
            FilterManager.AddBlock(INVBLOCK, FilterManager.Invis);
            FilterManager.AddBlock(NONEBLOCK, FilterManager.Error);
            Grouping = new TintFilterShadeGrouping([
                (FilterManager.Invis, TintManager.NullTint, true),
                (FilterManager.Error, TintManager.NullTint, true),
                ]);


            //groupManager.AddBlockToFilter("minecraft:air", groupManager.filter_invis, true);
            //BlocksManager = new Dictionary<ushort, uint> {
            //    { Block.GetId("minecraft:air"), 0 }
            //};

            if(rawmap != null) {
                foreach(var b in rawmap.blocks) {
                    uint color = 0xFF000000 | b.Value.color.ToUInt();

                    ushort id = Block.GetId(b.Key);
                    BlocksManager[id] = color;

                    if(b.Value.color.A < 255) {
                        FilterManager.AddBlock(id, FilterManager.Invis, true);
                    }
                }


                foreach(var t in rawmap.tints) {
                    var sprite = t.image?.ToBitmapSource()?.ToUIntMatrix();
                    Tint tint = TintManager.NullTint;
                    var format = TintMeta.GetFormat(t.format);
                    if(format != null) {
                        if(format.tintclass == typeof(OrthodoxVanillaTint)) tint = new OrthodoxVanillaTint(TintManager, t.name, world_version, sprite, datapacksInfo);
                        else if(format.tintclass == typeof(Vanilla_Grass)) tint = new Vanilla_Grass(TintManager, t.name, world_version, sprite, datapacksInfo);
                        else if(format.tintclass == typeof(Vanilla_Foliage)) tint = new Vanilla_Foliage(TintManager, t.name, world_version, sprite, datapacksInfo);
                        else if(format.tintclass == typeof(Vanilla_Water)) tint = new Vanilla_Water(TintManager, t.name, world_version, sprite, datapacksInfo);
                        else if(format.tintclass == typeof(GridTint)) tint = new GridTint(TintManager, t.name, t.yOffset, sprite);
                        else if(format.tintclass == typeof(FixedTint)) tint = new FixedTint(TintManager, t.name, t.color.ToUInt());
                    }

                    TintManager.ELEMENTS.Add(tint);

                    foreach(var block in t.blocks) {
                        if(Block.TryGetId(block.minecraftname(), out ushort id)) {
                            TintManager.AddBlock(id, tint);
                        }
                    }
                }


                foreach(var f in rawmap.filters) {
                    var filter = new Filter(FilterManager, f.name, true, true) { ABSORBTION = (int)(Math.Round(15 - f.transparency * 15)) };

                    FilterManager.ELEMENTS.Add(filter);

                    foreach(var block in f.blocks) {
                        if(Block.TryGetId(block.minecraftname(), out ushort id)) {
                            FilterManager.AddBlock(id, filter);
                        }
                    }
                }
            }


            BlocksManager = BlocksManager.ToFrozenDictionary();
            Block.Freeze();
            Biome.Freeze();
            Block.LoadOldBlocks();

            this.depth = rawmap != null ? Block.GetId(rawmap.depth) : NONEBLOCK;
            FilterManager.AddBlock(depth, FilterManager.Depth);

            this.noShades = new HashSet<ushort>(rawmap.no3dshadeblocks.Select(blname => Block.GetId(blname)).Where(blid => FilterManager.GetBlockVal(blid) != FilterManager.Invis)).ToFrozenSet();
            //
        }

        public uint BaseColor(ushort block) => block switch { 
            ERRORBLOCK => 0xFFFF0000,
            INVBLOCK =>   0x00000000,
            NONEBLOCK =>  0xFF0000FF,
            _ => BlocksManager.GetValueOrDefault(block, (uint)0)
        };
        public uint FullColor(ushort block, ushort biome, short height) => TintManager.GetBlockVal(block).GetTintedColor(BaseColor(block), biome, height);



        public bool AirHeightmapCompatible, WaterHeightmapCompatible;
        public void UpdateHeightmapCompatability() {
            AirHeightmapCompatible = true; 
            WaterHeightmapCompatible = true;

            foreach(var airblock in HeightmapFilter.AIRBLOCKS) {
                ushort id = Block.GetId(airblock);
                if(FilterManager.GetBlockVal(id).ABSORBTION > 0) {
                    AirHeightmapCompatible = false;
                    break;
                }
            }

            if(Block.GetId(HeightmapFilter.WATERBLOCK) != depth) WaterHeightmapCompatible = false;
            /*
            foreach(var waterinvblock in HeightmapFilter.WATERINVBLOCKS) {
                ushort id = Block.GetId(waterinvblock);
                if(FilterManager.GetBlockVal(id).ABSORBTION > 0) {
                    WaterHeightmapCompatible = false;
                    break;
                }
            }
            */
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



    public class GroupManager<T> : StandardizedSettings where T : GroupElement<T> {
        public readonly Colormap colormap;
        private IDictionary<ushort, T> map;
        public GroupManager(Colormap colormap) {
            this.colormap = colormap;

            Elements = new ObservableCollection<T>();
            ELEMENTS = new ObservableCollection<T>();

            Elements.CollectionChanged += (o, e) => {
                OnAutoChange(nameof(Elements));
            };
            ELEMENTS.CollectionChanged += (o, e) => {
                OnAutoChange(nameof(ELEMENTS));
            };

            map = new Dictionary<ushort, T>();
        }
        protected void InitDef(T origin) { this.Default = origin; ELEMENTS.Add(this.Default); }

        public T Default { get; private set; }
        public ObservableCollection<T> Elements, ELEMENTS;

        public override void SetFromBack() {
            ELEMENTS.Clear();
            foreach(var el in Elements) ELEMENTS.Add(el);
        }
        public override void Reset() {
            Elements.Clear();
            foreach(var el in ELEMENTS) Elements.Add(el);
        }
        public override bool ChangedBack() =>
                   (Elements.All(ELEMENTS.Contains) && Elements.Count == ELEMENTS.Count) == false;



        public void AddBlock(ushort block, T el = null, bool neww = false) {
            if(el == null) el = Default;
            if(neww == false) {
                foreach(var flt in Elements) {
                    if(flt.Blocks.Contains(block)) {
                        flt._RemoveBlock(block);
                        break;
                    }
                }
            }

            el._AddBlock(block);
        }

        public void RemoveBlock(ushort block, T el) {
            Default._AddBlock(block);
            el._RemoveBlock(block);
        }


        public void SetBlockVal(ushort id, T val) {
            if(map is FrozenDictionary<ushort, T>) map = new Dictionary<ushort, T>(map);
            map[id] = val;
        }
        public T GetBlockVal(ushort id) {
            if(map is Dictionary<ushort, T>) map = map.ToFrozenDictionary();

            if(map.TryGetValue(id, out var val)) return val;
            else return Default;
        }
    }
    public class TintManager : GroupManager<Tint> {
        public readonly Tint NullTint;
        public TintManager(Colormap colormap) : base(colormap) {
            this.NullTint = new NullTint(this);
            this.InitDef(NullTint);
        }
        public List<Tint> GetBlendingTints() => ELEMENTS.Where(t => t.GetBlendMode() == Blending.biomeonly || t.GetBlendMode() == Blending.full).ToList();
    }
    public class FilterManager : GroupManager<Filter> {
        public readonly Filter Solid, Depth, Invis, Error;
        public FilterManager(Colormap colormap) : base(colormap) {
            this.Solid = new Filter(this, "solid", false, false) { ABSORBTION = 15 };
            this.Depth = new Filter(this, "depth", true, true) { ABSORBTION = 15 };
            this.Invis = new Filter(this, "invis", true, false) { ABSORBTION = 0 };
            this.Error = new Filter(this, "error", false, false) { ABSORBTION = 15 };
            this.InitDef(Solid);
            base.ELEMENTS.Add(Depth);
            base.ELEMENTS.Add(Invis);
            base.ELEMENTS.Add(Error);
        }
    }
    public class TintFilterShadeGrouping {
        private readonly List<(Filter gr, Tint tint, bool shade)> Pairs;
        private IDictionary<(Filter gr, Tint tint, bool shade), int> PairsIndexes;

        private (Filter gr, Tint tint, bool shade)[] predefined;
        public TintFilterShadeGrouping((Filter gr, Tint tint, bool shade)[] predefined) {
            Pairs = new List<(Filter gr, Tint tint, bool shade)>();
            PairsIndexes = new Dictionary<(Filter gr, Tint tint, bool shade), int>();

            this.predefined = predefined;
            foreach(var predef in predefined) {
                Pairs.Add(predef);
                PairsIndexes.TryAdd(predef, Pairs.Count - 1);
            }
        }


        int i = 0;
        public (Filter filter, Tint tint, bool shade) GetGroup(int id) => Pairs[id];
        public int GetId(Filter filter, Tint tint, bool shade) {
            //i++;
            //if(i > 1500) PairsIndexes = PairsIndexes.ToFrozenDictionary();
            if(PairsIndexes.TryGetValue((filter, tint, shade), out int otg)) return otg;
            lock(this) {
                if(PairsIndexes.TryGetValue((filter, tint, shade), out int otgnow)) return otgnow;

                /*
                i = 0;
                if(PairsIndexes is FrozenDictionary<(Filter gr, Tint tint, bool shade), int>) {
                    PairsIndexes = new Dictionary<(Filter gr, Tint tint, bool shade), int>(PairsIndexes);
                    f++;
                }*/

                Pairs.Add((filter, tint, shade));
                PairsIndexes.TryAdd((filter, tint, shade), Pairs.Count - 1);
                return Pairs.Count - 1;
            }
        }
        public void Reset() {
            Pairs.Clear();
            PairsIndexes.Clear();
            i = 0;

            foreach(var predef in predefined) {
                Pairs.Add(predef);
                PairsIndexes.TryAdd(predef, Pairs.Count - 1);
            }
        }
    }


    public abstract class GroupElement<T> : StandardizedSettings where T : GroupElement<T> {
        protected GroupManager<T> groupManager;
        public readonly string name;
        public readonly HashSet<ushort> Blocks;
        public readonly HashSet<ushort> BLOCKS;

        public GroupElement(GroupManager<T> groupManager, string name) {
            this.groupManager = groupManager;
            this.name = name;

            Blocks = new HashSet<ushort>();
            BLOCKS = new HashSet<ushort>();
        }

        public void _AddBlock(ushort block) { 
            Blocks.Add(block);
            OnAutoChange(nameof(Blocks));

            if(SettingsHub == null) {
                BLOCKS.Add(block);
                OnAutoChange(nameof(BLOCKS));
                groupManager.SetBlockVal(block, (T)this);
            }
        }
        public void _RemoveBlock(ushort block) {
            Blocks.Remove(block);
            OnAutoChange(nameof(Blocks));

            if(SettingsHub == null) {
                BLOCKS.Add(block);
                OnAutoChange(nameof(BLOCKS));
            }
        }


        public override void SetFromBack() {
            InternalSetFromBack();

            BLOCKS.Clear();
            foreach(var bl in Blocks) {
                BLOCKS.Add(bl);
                groupManager.SetBlockVal(bl, (T)this);
            }
            OnHardChange(nameof(BLOCKS));
        }
        public override void Reset() {
            InternalReset();

            Blocks.Clear();
            Blocks.UnionWith(BLOCKS);
            OnAutoChange(nameof(Blocks));
        }
        public override bool ChangedBack() =>
                   InternalChangedBack() ||
                   !Blocks.ValueCompare(BLOCKS);


        public virtual void InternalSetFromBack() { }
        public virtual void InternalReset() { }
        public virtual bool InternalChangedBack() => false;
    }

}
