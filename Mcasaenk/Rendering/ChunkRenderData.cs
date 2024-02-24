using Accessibility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Animation;

namespace Mcasaenk.Rendering {

    public interface IChunkRenderData {
        ushort GetBiome(int cx, int cz, int cy, int i);
        ushort GetBlock(int cx, int cz, int cy, int i);
        bool CanSkipSection(int i);
        bool ContainsInformation();
    }

    public class ChunkRenderData117 : IChunkRenderData, IDisposable {

        private int[] biomes;
        private int biomeSize;
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
                            } else return false;
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
            for(int i = 0; i < blockStates.Length; i++) {
                if(blockStates[i] != null) pool.blockstates.Return(blockStates[i], false);
            }

            GC.SuppressFinalize(this);
        }

        public bool ContainsInformation() {
            return !error && hassections;
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

            int paletteIndex = getPaletteIndex(getIndex(cx, cy, cz, 16), blockStates[y[i + 4]], bits);
            return palettes[y[i + 4]][paletteIndex];
        }


        private int getIndex(int x, int y, int z, int stride) {
            return y * stride * stride + z * stride + x;
        }
        private int getPaletteIndex(int index, long[] blockStates, int bits) {
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
            return biomes[getIndex(biomeX / 4, biomeY / 4, biomeZ / 4, 4)];
        }

    }
}
