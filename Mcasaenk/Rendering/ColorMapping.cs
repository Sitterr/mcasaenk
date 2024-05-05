using Accessibility;
using Mcasaenk.Rendering.ChunkRenderData;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Mcasaenk.Rendering {
    public class NameToIdBiMap {
        private IDictionary<string, ushort> nameToId = new Dictionary<string, ushort>();
        private IDictionary<ushort, string> idToName = new Dictionary<ushort, string>();
        private ushort counter;
        private bool frozen;

        private string defaultName;
        private readonly Action<string, ushort> onAdd;
        public NameToIdBiMap(string defaultName, Action<string, ushort> onAdd) { 
            this.onAdd = onAdd;
            this.defaultName = defaultName;
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

            assignNew(defaultName);
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

    public static class ColorMapping {

        public static ushort BLOCK_AIR, BLOCK_WATER;

        public static readonly NameToIdBiMap 
            Block = new("minecraft:air", (name, id) => {
                switch(name) {
                    case "minecraft:air":
                        BLOCK_AIR = id;
                        break;
                    case "minecraft:water":
                        BLOCK_WATER = id;
                        break;
                }
            }), 
            Biome = new("plains", (name, id) => {
                switch(name) { 
                }
            });


        private static IDictionary<int, ushort> oldBiomeIdToName;
        public static ushort GetBiomeByOldId(int oldid) {
            if(oldBiomeIdToName.TryGetValue(oldid, out var id)) return id;
            return oldBiomeIdToName[-1];
        }
        private static void SetOldBiomeIds() {
            oldBiomeIdToName = new Dictionary<int, ushort>();
            TxtFormatReader.ReadStandartFormat(Resources.ResourceMapping.biome_names, (_, parts) => {
                int id = Convert.ToInt32(parts[0]);
                string name = parts[1];
                oldBiomeIdToName.Add(id, ColorMapping.Biome.GetId(name));
            });
            oldBiomeIdToName = oldBiomeIdToName.ToFrozenDictionary();
        }

        public static void Init() {
            Block.Reset();
            Biome.Reset();
            SetOldBiomeIds();
            Current = FromEnum.Mapping(Settings.COLOR_MAPPING_MODE);
            Block.Freeze();
            Biome.Freeze();
        }      






        public static IColorMapping Current { get; private set; }
    }


    public interface IColorMapping {
        uint GetColor(ushort block, ushort biome);

        ISet<ushort>[] GetTintGroups();
    }

    public class MapColorMapping : IColorMapping {
        private IDictionary<ushort, uint> colorMap;
        public MapColorMapping() {
            colorMap = new Dictionary<ushort, uint>();
            TxtFormatReader.ReadStandartFormat(Resources.ResourceMapping.block_colors_map, (_, parts) => {
                string name = parts[0];
                if(!name.Contains(":")) name = "minecraft:" + name;

                var hex = (uint)Convert.ToInt32(parts[1], 16);
                if(hex == 0) return;
                uint color = 0xFF000000 | hex;

                colorMap.Add(ColorMapping.Block.GetId(name), color);
            });
            colorMap = colorMap.ToFrozenDictionary();
        }

        public uint GetColor(ushort blockname, ushort biome) {
            if(colorMap.TryGetValue(blockname, out var color)) return color;
            return 0x00000000;
        }

        public ISet<ushort>[] GetTintGroups() => [];
    }

    public class MeanColorMapping : IColorMapping {
        private IDictionary<ushort, uint> colorMap;
        private ISet<ushort> grassBlocks, foliageBlocks, waterBlocks;
        private IDictionary<ushort, (uint grassTint, uint foliageTint, uint waterTint)> tintMap;

        private static uint WATERCOLOR = 0xff3359a2;

        public MeanColorMapping() {
            colorMap = new Dictionary<ushort, uint>();
            grassBlocks = new HashSet<ushort>(); foliageBlocks = new HashSet<ushort>(); waterBlocks = new HashSet<ushort>();
            tintMap = new Dictionary<ushort, (uint grassTint, uint foliageTint, uint waterTint)>();

            //colorMap.Add(ColorMapping.Block.GetId("minecraft:water"), WATERCOLOR);

            TxtFormatReader.ReadStandartFormat(Resources.ResourceMapping.block_colors_mean, (group, parts) => {
                switch(group) {
                    case "BIOMES": {
                        string name = parts[0];
                        uint grassTint = Global.ToARGBInt(parts[1]), foliageTint = Global.ToARGBInt(parts[2]), waterTint = Global.ToARGBInt(parts[3]);
                        tintMap.Add(ColorMapping.Biome.GetId(parts[0]), (grassTint, foliageTint, waterTint));
                        break;
                    }
                    case "COLORS": {
                        string name = parts[0];
                        if(!name.Contains(":")) name = "minecraft:" + name;
                        uint color = Global.ToARGBInt(parts[1]);
                        colorMap.TryAdd(ColorMapping.Block.GetId(name), color);
                        break;
                    }
                    case "GRASS": {
                        string name = parts[0];
                        if(!name.Contains(":")) name = "minecraft:" + name;
                        grassBlocks.Add(ColorMapping.Block.GetId(name));
                        break;
                    }
                    case "FOLIAGE": {
                        string name = parts[0];
                        if(!name.Contains(":")) name = "minecraft:" + name;
                        foliageBlocks.Add(ColorMapping.Block.GetId(name));
                        break;
                    }
                    case "WATER": {
                        string name = parts[0];
                        if(!name.Contains(":")) name = "minecraft:" + name;
                        waterBlocks.Add(ColorMapping.Block.GetId(name));
                        break;
                    }
                }
            });

            colorMap = colorMap.ToFrozenDictionary();
            tintMap = tintMap.ToFrozenDictionary();

            grassBlocks = grassBlocks.ToFrozenSet();
            foliageBlocks = foliageBlocks.ToFrozenSet();
            waterBlocks = waterBlocks.ToFrozenSet();
        }

        public uint GetColor(ushort blockname, ushort biome) {
            if(!colorMap.TryGetValue(blockname, out var color)) return 0x00000000;

            if(grassBlocks.Contains(blockname)) {
                color = applyTint(color, tintMap[biome].grassTint);
            } else if(foliageBlocks.Contains(blockname)) {
                color = applyTint(color, tintMap[biome].foliageTint);
            } else if(waterBlocks.Contains(blockname)) {
                color = applyTint(color, tintMap[biome].waterTint);
            }
            return color;
        }

        uint applyTint(uint color, uint tint) {
            uint nr = (tint >> 16 & 0xFF) * (color >> 16 & 0xFF) >> 8;
            uint ng = (tint >> 8 & 0xFF) * (color >> 8 & 0xFF) >> 8;
            uint nb = (tint & 0xFF) * (color & 0xFF) >> 8;
            return color & 0xFF000000 | nr << 16 | ng << 8 | nb;
        }


        public ISet<ushort>[] GetTintGroups() {
            return [grassBlocks, foliageBlocks];
        }
    }


}
