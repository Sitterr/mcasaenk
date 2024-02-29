using System.Diagnostics;

namespace Mcasaenk.Rendering {

    public interface IChunkRenderData {
        ushort GetBiome(int cx, int cz, int cy, int i);
        ushort GetBlock(int cx, int cz, int cy, int i);

        short GetHeight(int cx, int cz);
        short GetTerrainHeight(int cx, int cz);

        bool CanSkipSection(int i);
        bool ContainsInformation();
        bool ContainsHeightmaps();
    }

    public class ChunkRenderData117 : IChunkRenderData, IDisposable {

        private int[] biomes;
        private int biomeSize;
        private long[] world_surface, ocean_floor;
        private byte[] y;
        private long[][] blockStates;
        private short[] blockStatesSize;
        private List<ushort>[] palettes;

        private GenerateTilePool pool;
        public ChunkRenderData117(GenerateTilePool pool, LazyNBTReader r) {
            this.pool = pool;

            y = new byte[24];
            blockStates = new long[24][];
            blockStatesSize = new short[24];
            palettes = new List<ushort>[24];
            for(int i = 0; i < 24; i++) {
                //blockStates[i] = new long[342];
                y[i] = 100;
                palettes[i] = new List<ushort>();
            }

            this.Populate(r);
        }
        private bool error, hassections;
        private void Populate(LazyNBTReader r) {
            hassections = false;
            error = false;

            try {

                var h0 = r.ReadHeader();
                r.ForreachCompound((h0c) => {
                    if(h0c.name == "Level") {
                        r.ForreachCompound((levelEl) => {
                            if(levelEl.name == "Biomes") {
                                int len = r.ReadInt();
                                biomeSize = len;
                                biomes = pool.chunk_biomes.Rent(len);
                                r.ReadIntArray(biomes, len);
                                return true;
                            } else if(levelEl.name == "Sections") {
                                r.ForreachList((sType, si) => {
                                    hassections = true;
                                    r.ForreachCompound((sectionEl) => {
                                        if(sectionEl.name == "Y") {
                                            var b = (sbyte)r.ReadByte();
                                            if(b >= -4 && b <= 19) {
                                                y[b + 4] = (byte)si;
                                            }
                                            return true;
                                        } else if(sectionEl.name == "BlockStates") {
                                            int len = r.ReadInt();
                                            blockStatesSize[si] = (short)len;
                                            blockStates[si] = pool.blockstates.Rent(len);
                                            r.ReadLongArray(blockStates[si], len);
                                            return true;
                                        } else if(sectionEl.name == "Palette") {
                                            r.ForreachList((pType, pi) => {
                                                r.ForreachCompound((piC) => {
                                                    if(piC.name == "Name") {
                                                        string str = r.ReadUTF8();
                                                        palettes[si].Add(ColorMapping.Block.GetId(str));
                                                        return true;
                                                    } else return false;
                                                });
                                            });
                                            return true;
                                        } else return false;
                                    });
                                });
                                return true;
                            } 
                            else if(levelEl.name == "Heightmaps") {
                                r.ForreachCompound((hm) => {
                                    if(hm.name == "OCEAN_FLOOR") {
                                        int l = r.ReadInt();
                                        Debug.Assert(l == 37);
                                        ocean_floor = pool.ocean_floor.Rent(37);
                                        r.ReadLongArray(ocean_floor, 37);
                                        return true;
                                    } else if(hm.name == "WORLD_SURFACE") {
                                        int l = r.ReadInt();
                                        Debug.Assert(l == 37);
                                        world_surface = pool.world_surface.Rent(37);
                                        r.ReadLongArray(world_surface, 37);
                                        return true;
                                    } else return false;
                                });
                                return true;
                            } 
                            else return false;
                        });
                        return true;
                    } else return false;
                });

            }
            catch(Exception e) {
                error = true;
                throw;
            }
        }
        public void Dispose() {
            if(biomes != null) pool.chunk_biomes.Return(biomes, false);
            if(world_surface != null) pool.world_surface.Return(world_surface, false);
            if(ocean_floor != null) pool.ocean_floor.Return(ocean_floor, false);
            for(int i = 0; i < blockStates.Length; i++) {
                if(blockStates[i] != null) pool.blockstates.Return(blockStates[i], false);
            }

            GC.SuppressFinalize(this);
        }

        public bool ContainsInformation() {
            return !error && hassections;
        }
        public bool ContainsHeightmaps() { 
            return world_surface != null && ocean_floor != null;
        }
        public bool CanSkipSection(int i) {
            if(y[i + 4] == 100) return true;
            if(blockStates[y[i + 4]] == null || palettes[y[i + 4]] == null) return true;
            return false;
        }


        public ushort GetBiome(int cx, int cz, int cy, int i) {
            return ColorMapping.GetBiomeByOldId(getBiomeAtBlock(this.biomes, cx, i * 16 + cy, cz));
        }

        public ushort GetBlock(int cx, int cz, int cy, int i) {
            int bits = (int)blockStatesSize[y[i + 4]] >> 6;       

            int paletteIndex = GetValueFromBitArray(getIndexXYZ(cx, cy, cz, 16), blockStates[y[i + 4]], bits);
            return palettes[y[i + 4]][paletteIndex];
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
        private int GetValueFromBitArray(int index, long[] blockStates, int bits) {
            int indicesPerLong = (int)(64D / bits);
            int blockStatesIndex = index / indicesPerLong;
            int startBit = index % indicesPerLong * bits;
            return (int)(blockStates[blockStatesIndex] >> startBit) & (Global.Pow2(bits) - 1);
        }
        private int getBiomeAtBlock(int[] biomes, int biomeX, int biomeY, int biomeZ) {
            if(biomes == null) {
                return -1;
            }
            if(biomeSize == 1536) {
                biomeY += 64; // adjust for negative y block coordinates
            } else if(biomeSize != 1024) { // still support 256 height
                return -1;
            }
            return biomes[getIndexXYZ(biomeX / 4, biomeY / 4, biomeZ / 4, 4)];
        }

    }
}
