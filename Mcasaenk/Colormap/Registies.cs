﻿using Mcasaenk.Resources;
using Mcasaenk.WorldInfo;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Mcasaenk.Global;

namespace Mcasaenk.Colormaping {
    public class DynamicNameToIdBiMap {
        protected static IDictionary<string, string> synonyms;
        static DynamicNameToIdBiMap() {
            synonyms = new Dictionary<string, string>();
            TxtFormatReader.ReadStandartFormat(ResourceMapping.synonymblocks, (_, _parts) => {
                var parts = _parts.Select(w => w.minecraftname()).ToList();

                var v = parts.Last();
                foreach(var p in parts) {
                    if(synonyms.ContainsKey(p)) {
                        v = synonyms[p]; break;
                    }
                }

                foreach(var p in parts) {
                    synonyms.TryAdd(p, v);
                }
            });
            synonyms = synonyms.Where(p => p.Key != p.Value).ToFrozenDictionary();
        }

        protected IDictionary<string, ushort> nameToId = new Dictionary<string, ushort>();
        protected IDictionary<ushort, string> idToName = new Dictionary<ushort, string>();
        private ushort counter;
        private bool frozen;

        protected ushort def;
        private readonly Action<string, ushort> onAdd;
        public DynamicNameToIdBiMap(ushort def, Action<string, ushort> onAdd) {
            this.def = def;
            this.onAdd = onAdd;
        }
        public void SetDef(ushort def) => this.def = def;

        public ushort GetId(string name) {
            if(nameToId.TryGetValue(name, out var id)) return id;
            if(synonyms.TryGetValue(name, out var realname)) return GetId(realname);
            else if(frozen == false) return assignNew(name);
            else return def;
        }


        public string GetName(ushort id) {
            if(id == Colormap.ERRORBLOCK) return "_error block_";
            if(idToName.TryGetValue(id, out string name)) return name;
            return "_unknown block_";
        }

        private ushort assignNew(string name) {
            if(synonyms.TryGetValue(name, out var realname)) {
                if(nameToId.TryGetValue(realname, out var id)) return id;

                nameToId.Add(realname, counter);
                onAdd(realname, counter);
                idToName.Add(counter, name);
                return counter++;
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
            def = Global.Settings.DEFBIOME;
        }

        public ushort GetId(int oldid) {
            if(oldBiomeIdToId.TryGetValue(oldid, out var newid)) return newid;
            return def;
        }

        public void SetUp(List<BiomeInfo> biomes) {
            foreach(var biome in biomes.OrderBy(b => b.id)) {
                GetId(biome.name);
            }

            oldBiomeIdToId = new Dictionary<int, ushort>();
            TxtFormatReader.ReadStandartFormat(ResourceMapping.oldbiomes, (_, parts) => {
                int id = Convert.ToInt32(parts[0]);
                string name = parts[1];
                if(name.Contains(":") == false) name = "minecraft:" + name;
                oldBiomeIdToId.Add(id, GetId(name));
            });
            oldBiomeIdToId = oldBiomeIdToId.ToFrozenDictionary();
        }
    }

    public class BlockRegistry : DynamicNameToIdBiMap {
        private static IDictionary<int, ushort> oldBlockIdToId;
        public BlockRegistry(ushort def, Action<string, ushort> onAdd) : base(def, onAdd) { }

        public ushort GetId(int oldid) {
            if(oldBlockIdToId.ContainsKey(oldid)) return oldBlockIdToId[oldid];
            return Colormap.NONEBLOCK;
        }

        public void LoadOldBlocks() {
            oldBlockIdToId = new Dictionary<int, ushort>();
            TxtFormatReader.ReadStandartFormat(ResourceMapping.oldblocks, (_, parts) => {
                string name = parts[0].minecraftname();
                if(nameToId.ContainsKey(name) || synonyms.ContainsKey(name)) {
                    int oldidpart1 = Convert.ToInt32(parts[1]) << 4;
                    if(parts[2].Length > 0) {
                        foreach(var s in parts[2].Split(',')) {
                            int oldid = oldidpart1 + Convert.ToInt32(s);
                            oldBlockIdToId[oldid] = GetId(name);
                        }
                    } else {
                        for(int i = 0; i < 16; i++) {
                            oldBlockIdToId[oldidpart1 + i] = GetId(name);
                        }
                    }
                }
            });
            oldBlockIdToId = oldBlockIdToId.ToFrozenDictionary();
        }
    }
}