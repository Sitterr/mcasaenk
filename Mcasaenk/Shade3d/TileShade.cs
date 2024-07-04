using Accessibility;
using Mcasaenk.Rendering;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Intrinsics.X86;
using System.Windows;
using System.Xml.XPath;
using static Mcasaenk.Rendering.GenerateTilePool;

namespace Mcasaenk.Shade3d {
    public class TileShade : GenDataEditor {
        private readonly Tile tile;
        private int shadeStride;
        public TileShade(Tile tile) : base(tile) {
            this.tile = tile;
        }

        private bool[] shadeValues; // 512x512x20
        private byte[] valuesLen; // 512x512

        private bool[] harvested;

        private GenData genData;
        public void Construct(RawData freeRaw, GenData genData) {
            lock(locker) {
                IsActive = true;
                this.genData = genData;
                this.shadeValues = freeRaw.shadeValues;
                this.valuesLen = freeRaw.shadeValuesLen;

                this.shadeStride = ShadeConstants.GLB.blockReachLenMax;

                _ = Recalc();

                harvested = new bool[ShadeConstants.GLB.regionReach.Count];
                int i = 0;
                foreach(var p in ShadeConstants.GLB.regionReach) {
                    int _iz = p.p.Z, _ix = p.p.X;

                    if(_ix == 0 && _iz == 0) harvested[i] = true;


                    if(tile.GetOrigin().GetTile(tile.pos + new Point2i(_ix * ShadeConstants.GLB.xp, _iz * ShadeConstants.GLB.zp)) == null) harvested[i] = true;

                    i++;
                }


                CheckDestruct();
            }
        }

        protected override bool ShouldDestruct() {
            return harvested.Contains(false) == false;
        }
        protected override void Destruct() {
            this.shadeValues = null;
            this.valuesLen = null;
            this.genData = null;
            harvested = null;
        }


        public void UpdateSelf(bool[] shadeFrame) {
            lock(locker) {
                if(!IsActive) return;

                int i = 0;
                foreach(var p in ShadeConstants.GLB.regionReach) {
                    int _iz = p.p.Z, _ix = p.p.X;

                    int offsetZ = ShadeConstants.GLB.nflowZ(_iz, 0, ShadeConstants.GLB.rZ) * 512;
                    int offsetX = ShadeConstants.GLB.nflowX(_ix, 0, ShadeConstants.GLB.rX) * 512;

                    if(tile.GetOrigin().GetTileShadeFrame(tile.pos + new Point2i(_ix * ShadeConstants.GLB.xp, _iz * ShadeConstants.GLB.zp))
                        .GetCombinedSuitableFrames(new Point2i(_ix, _iz), shadeFrame, offsetX, offsetZ, ShadeConstants.GLB.rX * 512)) harvested[i] = true;

                    i++;
                }

                int x0 = ShadeConstants.GLB.nflowX(0, 0, ShadeConstants.GLB.rX) * 512;
                int z0 = ShadeConstants.GLB.nflowZ(0, 0, ShadeConstants.GLB.rZ) * 512;
                int SHADEX = ShadeConstants.GLB.rX * 512, SHADEZ = ShadeConstants.GLB.rZ * 512;
                for(int cz = 0; cz < 512; cz++) {
                    for(int cx = 0; cx < 512; cx++) {
                        int regionIndex = cz * 512 + cx;

                        int h = ShadeConstants.GLB.Height - genData.heights(regionIndex); //!!!
                        double x1 = (x0 + cx) + ShadeConstants.GLB.cosAcotgB * h, z1 = (z0 + cz) + -ShadeConstants.GLB.sinAcotgB * h;


                        if(genData.heights(regionIndex) != 0) {
                            byte a = 3;
                            ChunkRenderer.SetShadeValuesLine(shadeFrame, shadeValues, ref a, regionIndex, SHADEX, SHADEZ, x1, z1);
                            Debug.Assert(a == valuesLen[regionIndex]);
                        }
                    }
                }

                if(Recalc()) tile.RegisterRedraw();
                CheckDestruct();
            }
        }

        public bool Recalc() {
            bool changes = false;
            for(int i = 0; i < shadeValues.Length / shadeStride; i++) {
                if(genData.isShade(i)) continue;
                int j;
                for(j = 0; j < valuesLen[i]; j++) {
                    if(shadeValues[i * shadeStride + j] == false) break;
                }
                if(j == valuesLen[i]) {
                    changes = true;
                    genData.Set_isShade(i, true);
                }
            }
            return changes;
        }
    }




    public class TileShadeFrames {
        public readonly ConcurrentDictionary<Point2i, (bool[] frame, bool[] harvested)> frames;

        private readonly Point2i pos;
        private readonly TileMap tileMap;
        public TileShadeFrames(TileMap tileMap, Point2i pos) {
            this.tileMap = tileMap;
            this.pos = pos;
            frames = new();
        }

        public static int br = 0;
        public void AddFrame(bool[] frame, Point2i dist) {
            bool[] harvested = new bool[ShadeConstants.GLB.regionReach.Count];
            //bool[] harvested = new bool[ShadeConstants.GLB.rX * ShadeConstants.GLB.rZ];
            Array.Fill(harvested, true);

            var tilepos = pos - new Point2i(dist.X * ShadeConstants.GLB.xp, dist.Z * ShadeConstants.GLB.zp);

            int i = 0;
            foreach(var p in ShadeConstants.GLB.regionReach) {
                int _iz = p.p.Z, _ix = p.p.X;

                if(_ix >= dist.X && _iz >= dist.Z) {
                    var pp = pos - new Point2i(_ix * ShadeConstants.GLB.xp, _iz * ShadeConstants.GLB.zp);
                    harvested[i] = (tileMap.GetTile(pp) == null);

                    var f = (pp - tilepos).abs();
                    if(!ShadeConstants.GLB.regionReach.ContainsP(f)) {
                        br++;
                        harvested[i] = true;
                    }
                    
                }
                if(_ix == dist.X && _iz == dist.Z) {
                    harvested[i] = true;
                }
                i++;
            }

            frames.TryAdd(dist, (frame, harvested));

            RemoveUnnecessary();
        }

        public bool GetCombinedSuitableFrames(Point2i d, bool[] shadeFrame, int offsetX, int offsetZ, int stride) {
            bool usedZeroZero = false;

            int i = 0, di = 0;
            foreach(var p in ShadeConstants.GLB.regionReach) {
                int zz = p.p.Z, xx = p.p.X;
                if(xx == d.X && zz == d.Z) { di = i; break; }
                i++;
            }

            foreach(var p in ShadeConstants.GLB.regionReach) {
                int zz = p.p.Z, xx = p.p.X;

                if(xx <= d.X && zz <= d.Z) {
                    if(xx == d.X && zz == d.Z) {
                        continue;
                    }

                    if(frames.TryGetValue(new Point2i(xx, zz), out var fr)) {
                        for(int lx = 0; lx < 512; lx++) {
                            for(int lz = 0; lz < 512; lz++) {
                                shadeFrame[(offsetZ + lz) * stride + offsetX + lx] |= fr.frame[lz * 512 + lx];
                            }
                        }

                        fr.harvested[di] = true;

                        if(xx == 0 && zz == 0) usedZeroZero = true;
                    }
                }

            }

            RemoveUnnecessary();
            return usedZeroZero;
        }

        public bool[] GetFrame(Point2i dist) {
            if(frames.TryGetValue(dist, out var fr)) {
                return fr.frame;
            } else return null;
        }

        private void RemoveUnnecessary() {
            var toRemove = frames.Where((p) => !p.Value.harvested.Contains(false)).Select(p => p.Key);
            foreach(var key in toRemove) {
                frames.TryRemove(key, out _);
            }
        }
    }
}
