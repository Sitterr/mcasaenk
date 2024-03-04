using CommunityToolkit.HighPerformance.Buffers;
using Mcasaenk.Nbt;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcasaenk.Rendering.ChunkRenderData._117 {
    public class ChunkDataInterpreter117 : IChunkInterpreter {
        private CompoundTag tag;

        private ArrTag<int> biomes;
        private ArrTag<long> world_surface, ocean_floor;

        private ArrTag<long>[] blockStates;
        private ushort[][] palettes;

        public ChunkDataInterpreter117(Tag _tag, bool error) {
            this.error = error;

            //try {
            this.tag = (CompoundTag)_tag;
            var level = (CompoundTag)tag["Level"];
            {
                biomes = (ArrTag<int>)level["Biomes"];

                var heightmaps = (CompoundTag)level["Heightmaps"];
                {
                    world_surface = (ArrTag<long>)heightmaps["WORLD_SURFACE"];
                    ocean_floor = (ArrTag<long>)heightmaps["OCEAN_FLOOR"];
                }

                blockStates = new ArrTag<long>[20 + 4];
                palettes = new ushort[20 + 4][];
                var sections = (ListTag)level["Sections"];
                if(sections != null) {
                    foreach(var _section in (List<Tag>)sections) {
                        var section = (CompoundTag)_section;

                        sbyte y = (sbyte)((NumTag<sbyte>)section["Y"] + 4);
                        if(y < 0) continue;
                        blockStates[y] = (ArrTag<long>)section["BlockStates"];

                        var palette = (ListTag)section["Palette"];
                        if(palette != null) {
                            palettes[y] = new ushort[palette.Length];
                            int i = 0;
                            foreach(var _p in (List<Tag>)palette) {
                                var p = (CompoundTag)_p;

                                var name = (NumTag<string>)p["Name"];
                                palettes[y][i] = ColorMapping.Block.GetId(name);
                                i++;
                            }
                        }

                    }
                }
            }


            //} catch {
            //    this.error = true;
            //}
        }
        private bool error;

        public bool ContainsInformation() {
            return !error;
        }
        public bool ContainsHeightmaps() {
            return world_surface != null && ocean_floor != null;
        }
        public bool CanSkipSection(int i) {
            if(blockStates[i + 4] == null || palettes[i + 4] == null) return true;
            return false;
        }


        public ushort GetBiome(int cx, int cz, int cy, int i) {
            return ColorMapping.GetBiomeByOldId(getBiomeAtBlock(cx, i * 16 + cy, cz));
        }

        public ushort GetBlock(int cx, int cz, int cy, int i) {
            int bits = blockStates[i + 4].Length >> 6;

            int paletteIndex = GetValueFromBitArray(getIndexXYZ(cx, cy, cz, 16), blockStates[i + 4], bits);
            return palettes[i + 4][paletteIndex];
        }
        public short GetHeight(int cx, int cz) {
            return (short)GetValueFromBitArray(getIndexXZ(cx, cz, 16), world_surface, 9);
        }
        public short GetTerrainHeight(int cx, int cz) {
            return (short)GetValueFromBitArray(getIndexXZ(cx, cz, 16), ocean_floor, 9);
        }

        private int getIndexXYZ(int x, int y, int z, int stride) {
            return y * stride * stride + z * stride + x;
        }
        private int getIndexXZ(int x, int z, int stride) {
            return getIndexXYZ(x, 0, z, stride);
        }
        private int GetValueFromBitArrayUninterrupted(int index, long[] blockStates, int bits) {
            throw new NotImplementedException();
        }
        private int GetValueFromBitArray(int index, ArrTag<long> blockStates, int bits) {
            int indicesPerLong = (int)(64D / bits);
            int blockStatesIndex = index / indicesPerLong;
            int startBit = index % indicesPerLong * bits;
            return (int)(blockStates[blockStatesIndex] >> startBit) & (Global.Pow2(bits) - 1);
        }
        private int getBiomeAtBlock(int biomeX, int biomeY, int biomeZ) {
            if(biomes == null) {
                return -1;
            }
            if(biomes.Length == 1536) {
                biomeY += 64; // adjust for negative y block coordinates
            } else if(biomes.Length != 1024) { // still support 256 height
                return -1;
            }
            return biomes[getIndexXYZ(biomeX / 4, biomeY / 4, biomeZ / 4, 4)];
        }


        public void Dispose() {
            tag.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
