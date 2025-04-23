using Mcasaenk.Colormaping;
using Mcasaenk.Nbt;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Mcasaenk.Rendering.ChunkRenderData {
    public class ChunkDataInterpreter118 : IChunkInterpreter {
        private static ArrayPool<ushort> pallattePool = ArrayPool<ushort>.Shared;

        private CompoundTag_Optimal tag;

        private ArrTag<long> world_surface, ocean_floor, motion_blocking;

        private ArrTag<long>[] blockStates, biomes;
        private ushort[][] blockStates_palette, biomes_palette;

        private ArrTag<byte>[] blocklights;

        int height, negy, negys;

        public Colormap Colormap { get; }
        public ChunkDataInterpreter118(Colormap colormap, Tag _tag, int miny, int height, bool error) {
            Colormap = colormap;
            int SECTIONS = (int)Math.Ceiling(height / (double)16);
            this.negy = -miny;
            this.negys = negy / 16;
            this.height = height;

            this.error = error;

            tag = (CompoundTag_Optimal)_tag;

            var heightmaps = (CompoundTag_Optimal)tag["Heightmaps"];
            if(heightmaps != null) {
                world_surface = (ArrTag<long>)heightmaps["WORLD_SURFACE"];
                ocean_floor = (ArrTag<long>)heightmaps["OCEAN_FLOOR"];
                motion_blocking = (ArrTag<long>)heightmaps["MOTION_BLOCKING"];
            }

            blockStates = new ArrTag<long>[SECTIONS];
            blockStates_palette = new ushort[SECTIONS][];
            biomes = new ArrTag<long>[SECTIONS];
            biomes_palette = new ushort[SECTIONS][];
            blocklights = new ArrTag<byte>[SECTIONS];
            var sections = (ListTag)tag["sections"];
            if(sections != null) {
                foreach(var _section in (List<Tag>)sections) {
                    var section = (CompoundTag_Optimal)_section;

                    sbyte y = (sbyte)((NumTag<sbyte>)section["Y"] + negys);
                    if(y < 0 || y >= blocklights.Length) continue;
                    blocklights[y] = (ArrTag<byte>)section["BlockLight"];

                    var blockStatesTag = (CompoundTag_Optimal)section["block_states"];
                    if(blockStatesTag != null) {
                        blockStates[y] = (ArrTag<long>)blockStatesTag["data"];
                        var palette = (ListTag)blockStatesTag["palette"];
                        if(palette != null) {
                            blockStates_palette[y] = pallattePool.Rent(palette.Length + 1);
                            if(blockStates[y] != null) blockStates_palette[y][0] = (ushort)(blockStates[y].Length >> 6);
                            else blockStates_palette[y][0] = 0;
                            int i = 1;
                            foreach(var _p in (List<Tag>)palette) {
                                var p = (CompoundTag_Optimal)_p;

                                var name = ((string)(NumTag<string>)p["Name"]).minecraftname();
                                bool waterlogged = false;

                                var properties = (CompoundTag_Optimal)p["Properties"];
                                if(properties != null) {
                                    var wl = properties["waterlogged"];
                                    if(wl != null) waterlogged = (NumTag<string>)wl == "true";
                                }
                                waterlogged |= Colormap.INHERENT_WATER_LOGGED.Contains(name);                             

                                ushort id = Colormap.Block.GetId(name);
                                if(waterlogged && BlockManager.depth == Colormap.BLOCK_WATER) id = Colormap.BLOCK_WATER;
                                blockStates_palette[y][i] = id;

                                i++;
                            }
                        }
                    }

                    var biomesTag = (CompoundTag_Optimal)section["biomes"];
                    if(biomesTag != null) {
                        biomes[y] = (ArrTag<long>)biomesTag["data"];
                        var palette = (ListTag)biomesTag["palette"];

                        biomes_palette[y] = pallattePool.Rent(palette.Length + 1);
                        biomes_palette[y][0] = (ushort)((ushort)Math.Floor(Math.Log(palette.Length - 1, 2)) + 1);
                        int i = 1;
                        foreach(var _p in (List<Tag>)palette) {
                            var p = (NumTag<string>)_p;

                            biomes_palette[y][i] = Colormap.Biome.GetId(p);
                            i++;
                        }
                    }

                }
            }
        }
        private bool error;

        public bool ContainsInformation() {
            return !error;
        }
        public bool ContainsHeightmaps() {
            return world_surface != null && ocean_floor != null;
        }
        public ushort SingleBlockSection(int i) {
            if(blockStates_palette[i] == null) return Colormap.BLOCK_AIR;
            if(blockStates[i] != null) return Colormap.NONEBLOCK;
            return blockStates_palette[i][0];
        }



        public ushort GetBiome(int cx, int cz, int cy) {
            if(cy < 0 || cy >= height) return default;
            int i = cy / 16;
            if(biomes_palette[i] == null) return 0;
            if(biomes[i] == null) return biomes_palette[i][0 + 1];


            int paletteIndex = (this as IChunkInterpreter).GetValueFromBitArray(getIndexXYZ(cx / 4, (cy % 16) / 4, cz / 4, 4), biomes[i], biomes_palette[i][0]);
            return biomes_palette[i][paletteIndex + 1];
        }

        public ushort GetBlock(int cx, int cz, int cy) {
            if(cy < 0 || cy >= height) return Colormap.INVBLOCK;
            int i = cy / 16;
            if(blockStates_palette[i] == null) return Colormap.INVBLOCK;
            if(blockStates[i] == null) return blockStates_palette[i][0 + 1];

            int paletteIndex = (this as IChunkInterpreter).GetValueFromBitArray(getIndexXYZ(cx, cy % 16, cz, 16), blockStates[i], blockStates_palette[i][0]);
            return blockStates_palette[i][paletteIndex + 1];
        }

        public short GetHeight(int cx, int cz) {
            return getHeight(world_surface, cx, cz);
        }
        public short GetMotionHeight(int cx, int cz) {
            return getHeight(motion_blocking, cx, cz);
        }

        public short GetTerrainHeight(int cx, int cz) {
            return getHeight(ocean_floor, cx, cz);
        }


        public byte GetBlockLight(int cx, int cz, int cy) {
            if(cy < 0 || cy >= height) return default;
            int i = cy / 16;
            if(blocklights[i] == null) return 0;

            int p = getIndexXYZ(cx, cy % 16, cz, 16);
            byte val = blocklights[i][p / 2];
            if(p % 2 == 1) {
                return (byte)(val >> 4);
            } else {
                return (byte)(val & 0x0F);
            }
        }



        private short getHeight(ArrTag<long> heightmap, int cx, int cz) {
            if(heightmap == null) return (short)-negy;
            short val = (short)((this as IChunkInterpreter).GetValueFromBitArray(getIndexXZ(cx, cz, 16), heightmap, 9) - 1);
            //val -= (short)negy;
            return val;
        }

        private int getIndexXYZ(int x, int y, int z, int stride) {
            return y * stride * stride + z * stride + x;
        }
        private int getIndexXZ(int x, int z, int stride) {
            return getIndexXYZ(x, 0, z, stride);
        }


        public void Dispose() {
            tag.Dispose();
            for(int i = 0; i < blockStates_palette.Length; i++) {
                if(blockStates_palette[i] != null) {
                    pallattePool.Return(blockStates_palette[i]);
                }
            }
            for(int i = 0; i < biomes_palette.Length; i++) {
                if(biomes_palette[i] != null) {
                    pallattePool.Return(biomes_palette[i]);
                }
            }
        }
    }
}
