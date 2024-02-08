using Accessibility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Animation;

namespace Mcasaenk.Rendering {
    public class ChunkRenderData117 : IDisposable {

        public int[] biomes;
        public int biomeSize;
        private byte[] y;
        private long[][] blockStates;
        public short[] blockStatesSize;
        private List<string>[] palettes;

        public List<string> palette(int i) {
            if(y[i] == 100) return null;
            return palettes[y[i]];
        }
        public long[] blockState(int i) {
            if(y[i] == 100) return null;
            return blockStates[y[i]];
        }
        public short blockStateSize(int i) {
            if(y[i] == 100) return 0;
            return blockStatesSize[y[i]];
        }

        private LazyNBTReader r;
        public ChunkRenderData117(LazyNBTReader r) {
            this.r = r;

            y = new byte[24];
            blockStates = new long[24][];
            blockStatesSize = new short[24];
            palettes = new List<string>[24];
            for(int i = 0; i < 24; i++) {
                //blockStates[i] = new long[342];
                y[i] = 100;
                palettes[i] = new List<string>();
            }



            this.Populate();
        }

        public void Dispose() {
            if(biomes != null) PoolHandler.biomes.Return(biomes, false);
            for(int i = 0; i < blockStates.Length; i++) {
                if(blockStates[i] != null) PoolHandler.blockstates.Return(blockStates[i], false);
            }
        }

        public bool ContainsInformation() {
            return !error && hassections;
        }

        private bool error, hassections;
        private void Populate() {
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
                                biomes = PoolHandler.biomes.Rent(len);
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
                                            blockStates[si] = PoolHandler.blockstates.Rent(len);
                                            r.ReadLongArray(blockStates[si], len);
                                            return true;
                                        } else if(sectionEl.name == "Palette") {
                                            r.ForreachList((pType, pi) => {
                                                r.ForreachCompound((piC) => {
                                                    if(piC.name == "Name") {
                                                        string str = r.ReadUTF8();
                                                        palettes[si].Add(str);
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
            }
        }



    }
}
