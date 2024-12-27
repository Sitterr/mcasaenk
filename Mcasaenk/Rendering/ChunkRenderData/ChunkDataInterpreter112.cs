using Mcasaenk.Colormaping;
using Mcasaenk.Nbt;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Mcasaenk.Rendering.ChunkRenderData {
    public class ChunkDataInterpreter112 : IChunkInterpreter {
        private CompoundTag_Optimal tag;
      
        private ArrTag<long> world_surface, ocean_floor, motion_blocking;

        private ArrTag<byte> biomes;
        private ArrTag<byte>[] blocks, data, blocklights;

        int height, negy, negys;

        public Colormap Colormap { get; }
        public ChunkDataInterpreter112(Colormap colormap, Tag _tag, int miny, int height, bool error) {
            this.Colormap = colormap;
            int SECTIONS = (int)Math.Ceiling(height / (double)16);
            this.negy = -miny;
            this.negys = negy / 16;
            this.height = height;

            this.error = error;

            //try {
            tag = (CompoundTag_Optimal)_tag;
            var level = (CompoundTag_Optimal)tag["Level"];
            {
                biomes = (ArrTag<byte>)level["Biomes"];

                var heightmaps = (CompoundTag_Optimal)level["Heightmaps"];
                if(heightmaps != null) {
                    world_surface = (ArrTag<long>)heightmaps["WORLD_SURFACE"];
                    ocean_floor = (ArrTag<long>)heightmaps["OCEAN_FLOOR"];
                    motion_blocking = (ArrTag<long>)heightmaps["MOTION_BLOCKING"];
                }

                blocks = new ArrTag<byte>[SECTIONS];
                data = new ArrTag<byte>[SECTIONS];
                blocklights = new ArrTag<byte>[SECTIONS];
                var sections = (ListTag)level["Sections"];
                if(sections != null) {
                    foreach(var _section in (List<Tag>)sections) {
                        var section = (CompoundTag_Optimal)_section;

                        sbyte y = (sbyte)((NumTag<sbyte>)section["Y"] + negys);
                        if(y < 0 || y >= SECTIONS) continue;
                        blocks[y] = (ArrTag<byte>)section["Blocks"];
                        data[y] = (ArrTag<byte>)section["Data"];
                        blocklights[y] = (ArrTag<byte>)section["BlockLight"];
                    }
                }

            }

            //} catch {
            //    this.error = true;
            //}
        }
        private bool error;

        public bool ContainsInformation() {
            if(blocks.All(b => b == null)) return false;
            return !error;
        }
        public bool ContainsHeightmaps() {
            return world_surface != null && ocean_floor != null;
        }
        public ushort SingleBlockSection(int i) {
            if(blocks[i] == null) return Colormap.INVBLOCK;
            return Colormap.NONEBLOCK;
        }



        public ushort GetBiome(int cx, int cz, int cy) {
            return Global.App.Colormap.Biome.GetId(getBiomeAtBlock(cx, cy, cz));
        }

        public ushort GetBlock(int cx, int cz, int cy) {
            if(cy < 0 || cy >= height) return default;
            int i = cy / 16;
            if(blocks[i] == null) return Colormap.INVBLOCK;
            if(blocks[i].Length != 4096) return Colormap.NONEBLOCK;

            int index = getIndexXYZ(cx, cy % 16, cz, 16), block = blocks[i][index] << 4;
            byte blockData = (byte)(index % 2 == 0 ? data[i][index / 2] & 0x0F : (data[i][index / 2] >> 4) & 0x0F);

            return Global.App.Colormap.Block.GetId(block + blockData);
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
        }
    }
}
