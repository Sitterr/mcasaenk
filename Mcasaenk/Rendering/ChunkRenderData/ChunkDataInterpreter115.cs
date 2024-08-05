using Mcasaenk.Nbt;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcasaenk.Rendering.ChunkRenderData {
    public class ChunkDataInterpreter115 : IChunkInterpreter {
        private static ArrayPool<ushort> pallattePool = ArrayPool<ushort>.Shared;

        private CompoundTag_Optimal tag;

        private ArrTag<int> biomes;
        private ArrTag<long> world_surface, ocean_floor, motion_blocking;

        private ArrTag<long>[] blockStates;
        private ushort[][] palettes;

        private ArrTag<byte>[] blocklights;

        int height, negy, negys;
        public ChunkDataInterpreter115(Tag _tag, int miny, int height, bool error) {
            int SECTIONS = (int)Math.Ceiling(height / (double)16);
            this.negy = -miny;
            this.negys = negy / 16;
            this.height = height;

            this.error = error;

            //try {
            tag = (CompoundTag_Optimal)_tag;
            var level = (CompoundTag_Optimal)tag["Level"];
            {
                biomes = (ArrTag<int>)level["Biomes"];

                var heightmaps = (CompoundTag_Optimal)level["Heightmaps"];
                if(heightmaps != null) {
                    world_surface = (ArrTag<long>)heightmaps["WORLD_SURFACE"];
                    ocean_floor = (ArrTag<long>)heightmaps["OCEAN_FLOOR"];
                    motion_blocking = (ArrTag<long>)heightmaps["MOTION_BLOCKING"];
                }

                blockStates = new ArrTag<long>[SECTIONS];
                blocklights = new ArrTag<byte>[SECTIONS];
                palettes = new ushort[SECTIONS][];
                var sections = (ListTag)level["Sections"];
                if(sections != null) {
                    foreach(var _section in (List<Tag>)sections) {
                        var section = (CompoundTag_Optimal)_section;

                        sbyte y = (sbyte)((NumTag<sbyte>)section["Y"] + negys);
                        if(y < 0 || y >= blockStates.Length) continue;
                        blockStates[y] = (ArrTag<long>)section["BlockStates"];
                        blocklights[y] = (ArrTag<byte>)section["BlockLight"];


                        var palette = (ListTag)section["Palette"];
                        if(palette != null) {
                            //palettes[y] = new ushort[palette.Length];
                            palettes[y] = pallattePool.Rent(palette.Length + 1);
                            if(blockStates[y] != null) palettes[y][0] = (ushort)(blockStates[y].Length >> 6);
                            else palettes[y][0] = 0;
                            int i = 1;
                            foreach(var _p in (List<Tag>)palette) {
                                var p = (CompoundTag_Optimal)_p;

                                var name = (string)(NumTag<string>)p["Name"];
                                bool waterlogged = false;

                                var properties = (CompoundTag_Optimal)p["Properties"];
                                if(properties != null) {
                                    var wl = properties["waterlogged"];
                                    if(wl != null) waterlogged = (NumTag<string>)wl == "true";
                                }
                                waterlogged |= Colormap.INHERENT_WATER_LOGGED.Contains(name.minecraftname());

                                ushort id = Global.App.Colormap.Block.GetId(name);
                                if(waterlogged && (id == Colormap.INVBLOCK || Global.App.Colormap.depth == Global.App.Colormap.BLOCK_WATER)) id = Global.App.Colormap.BLOCK_WATER;
                                palettes[y][i] = id;

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
            if(blockStates.All(b => b == null)) return false;
            return !error;
        }
        public bool ContainsHeightmaps() {
            return world_surface != null && ocean_floor != null;
        }
        public ushort SingleBlockSection(int i) {
            if(blockStates[i] == null && palettes[i] == null) return Global.App.Colormap.BLOCK_AIR;
            return Colormap.NONEBLOCK;
        }



        public ushort GetBiome(int cx, int cz, int cy) {
            return Global.App.Colormap.Biome.GetId(getBiomeAtBlock(cx, cy, cz));
        }

        public ushort GetBlock(int cx, int cz, int cy) {
            if(cy < 0 || cy >= height) return Colormap.INVBLOCK;
            int i = cy / 16;
            if(blockStates[i] == null || palettes[i] == null) return Colormap.INVBLOCK;
            if(blockStates[i] == null) return palettes[i][0 + 1];

            int paletteIndex = (this as IChunkInterpreter).GetValueFromBitArrayUninterrupted(getIndexXYZ(cx, cy % 16, cz, 16), blockStates[i], palettes[i][0]);
            return palettes[i][paletteIndex + 1];
        }

        public short GetHeight(int cx, int cz) => getHeight(world_surface, cx, cz);
        public short GetMotionHeight(int cx, int cz) => getHeight(motion_blocking, cx, cz);
        public short GetTerrainHeight(int cx, int cz) => getHeight(ocean_floor, cx, cz);


        public byte GetBlockLight(int cx, int cz, int cy) {
            if(cy < 0 || cy >= height) return default;
            int i = cy / 16;
            if(blocklights[i] == null) return 0;
            if(blocklights[i].Length == 0) return 15;

            int p = getIndexXYZ(cx, cy % 16, cz, 16);
            byte val = blocklights[i][p / 2];
            if(p % 2 == 1) {
                return (byte)(val >> 4);
            } else {
                return (byte)(val & 0x0F);
            }
        }



        private short getHeight(ArrTag<long> heightmap, int cx, int cz) {
            if(heightmap == null) return -1;
            short val = (short)((this as IChunkInterpreter).GetValueFromBitArrayUninterrupted(getIndexXZ(cx, cz, 16), heightmap, 9) - 1);
            return val;
        }

        private int getIndexXYZ(int x, int y, int z, int stride) {
            return y * stride * stride + z * stride + x;
        }
        private int getIndexXZ(int x, int z, int stride) {
            return getIndexXYZ(x, 0, z, stride);
        }
        private int getBiomeAtBlock(int biomeX, int biomeY, int biomeZ) {
            if(biomes == null || biomeY < 0) return default;
            return biomes[getIndexXZ(biomeX, biomeZ, 16)];
        }


        public void Dispose() {
            tag.Dispose();
            for(int i = 0; i < palettes.Length; i++) {
                if(palettes[i] != null) {
                    pallattePool.Return(palettes[i]);
                }
            }
        }
    }
}
