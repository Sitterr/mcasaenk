using Accessibility;
using Mcasaenk.Rendering;
using System;
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

        private byte[][] shadeValues; // colx512x512x20

        private bool[] harvested;

        private GenData genData;
        public void Construct(RawData freeRaw, GenData genData) {
            lock(locker) {
                IsActive = true;
                this.genData = genData;
                this.shadeValues = freeRaw.columns.Append(freeRaw.depthColumn).Select(c => c.shadeValues).ToArray();

                this.shadeStride = ShadeConstants.GLB.blockReachLenMax;

                _ = Recalc();

                harvested = new bool[ShadeConstants.GLB.regionReach.Length];
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
            this.genData = null;
            harvested = null;
        }


        public void UpdateSelf(byte[] shadeFrame) {
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
                for(int w = 0; w < genData.columns.Length; w++) {
                    if(shadeValues[w] == null) continue;
                    for(int cz = 0; cz < 512; cz++) {
                        for(int cx = 0; cx < 512; cx++) {
                            int regionIndex = cz * 512 + cx;
                            if(genData.columns[w].ContainsInfo(regionIndex) == false) continue;

                            int h = ShadeConstants.GLB.Height - genData.columns[w].Height(regionIndex); //!!!
                            if(w == genData.columns.Length - 1) h = ShadeConstants.GLB.Height - (genData.columns[w].Height(regionIndex) + genData.columns[w].Depth(regionIndex));
                            double x1 = (x0 + cx) + ShadeConstants.GLB.cosAcotgB * h, z1 = (z0 + cz) + -ShadeConstants.GLB.sinAcotgB * h;


                            //if(genData.columns[w].Height(regionIndex) != 0) {
                                ChunkRenderer.SetShadeValuesLine(shadeFrame, shadeValues[w], regionIndex, SHADEX, SHADEZ, (int)x1, (int)z1);
                            //}
                        }
                    }
                }

                if(Recalc()) tile.RegisterRedraw();
                CheckDestruct();
            }
        }

        public bool Recalc() {
            bool changes = false;
            for(int w = 0; w < this.shadeValues.Length; w++) {
                var shadeValues = this.shadeValues[w];

                if(shadeValues == null) continue;
                
                for(int i = 0; i < 512 * 512; i++) {
                    if(genData.columns[w].ContainsInfo(i) == false) continue;
                    if(genData.columns[w].Shade(i) == 15) continue;

                    byte min = 15;
                    for(int j = 0; j < ShadeConstants.GLB.blockReachLenMax; j++) {
                        byte val = Math.Max(ShadeConstants.GetLeft(shadeValues, i * shadeStride + j), ShadeConstants.GetRight(shadeValues, i * shadeStride + j));

                        min = Math.Min(min, val);
                    }

                    byte shade = min;
                    if(shade > genData.columns[w].Shade(i)) {
                        changes = true;
                        genData.columns[w].set_shade(i, shade);
                    }

                }
            }

            return changes;
        }
    }


    public class TileShadeFrames {
        public readonly ConcurrentDictionary<Point2i, (byte[] frame, bool[] harvested)> frames;

        private readonly Point2i pos;
        private readonly TileMap tileMap;
        public TileShadeFrames(TileMap tileMap, Point2i pos) {
            this.tileMap = tileMap;
            this.pos = pos;
            frames = new();
        }

        public void AddFrame(byte[] frame, Point2i dist) {
            bool[] harvested = new bool[ShadeConstants.GLB.regionReach.Length];
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
                    if(!ShadeConstants.GLB.regionReach.Any(r => r.p == f)) {
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

        public bool GetCombinedSuitableFrames(Point2i d, byte[] shadeFrame, int offsetX, int offsetZ, int stride) {
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
                                byte left = ShadeConstants.CombineShades(ShadeConstants.GetLeft(shadeFrame, (offsetZ + lz) * stride + offsetX + lx), ShadeConstants.GetLeft(fr.frame, lz * 512 + lx)),
                                    right = ShadeConstants.CombineShades(ShadeConstants.GetRight(shadeFrame, (offsetZ + lz) * stride + offsetX + lx), ShadeConstants.GetRight(fr.frame, lz * 512 + lx));
                                ShadeConstants.SetBoth(shadeFrame, (offsetZ + lz) * stride + offsetX + lx, left, right);
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

        public byte[] GetFrame(Point2i dist) {
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
