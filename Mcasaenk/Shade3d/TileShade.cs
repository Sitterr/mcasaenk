using Mcasaenk.Rendering;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Intrinsics.X86;
using System.Windows;

namespace Mcasaenk.Shade3d {
    public class TileShade {
        private readonly Tile tile;
        private int shadeStride;
        public TileShade(Tile tile) {
            this.tile = tile;
            active = false;
        }

        private short[] heightMap; // 512x512 waterHeights

        private bool[] isShade; // 512x512
        private bool[] shadeValues; // 512x512x20
        private byte[] valuesLen; // 512x512
        public bool[] shouldShade; // 512x512

        private bool shouldRedraw;
        public bool active;

        private bool[] harvested;

        public object locker = new object();

        public void Construct(short[] heightMap, bool[] shadeValues, byte[] valuesLen) {
            this.heightMap = heightMap;
            this.isShade = new bool[512 * 512];
            this.shouldShade = new bool[512 * 512];
            this.shadeValues = shadeValues;
            this.valuesLen = valuesLen;

            this.shadeStride = ShadeConstants.GLB.blockReachLenMax;

            harvested = new bool[ShadeConstants.GLB.rX * ShadeConstants.GLB.rZ];
            for(int _ix = 0; _ix < ShadeConstants.GLB.rX; _ix++) {
                for(int _iz = 0; _iz < ShadeConstants.GLB.rZ; _iz++) {
                    if(tile.GetOrigin().GetTile(tile.pos + new Point2i(_ix * ShadeConstants.GLB.xp, _iz * ShadeConstants.GLB.zp)) == null) harvested[_iz * ShadeConstants.GLB.rX + _ix] = true;
                }
            }
            harvested[0] = true;

            RecalcIsShade(true);
            active = true;
        }
        private void Destruct() {
            this.heightMap = null;
            this.isShade = null;
            this.shadeValues = null;
            this.valuesLen = null;
            this.shouldShade = null;

            active = false;
        }


        public void UpdateInfo(bool[] shadeFrame) {
            lock(locker) {
                if(!active) return;

                for(int _ix = 0; _ix < ShadeConstants.GLB.rX; _ix++) {
                    for(int _iz = 0; _iz < ShadeConstants.GLB.rZ; _iz++) {
                        int offsetZ = ShadeConstants.GLB.nflowZ(_iz, 0, ShadeConstants.GLB.rZ) * 512;
                        int offsetX = ShadeConstants.GLB.nflowX(_ix, 0, ShadeConstants.GLB.rX) * 512;

                        if(tile.GetOrigin().GetTileShadeFrame(tile.pos + new Point2i(_ix * ShadeConstants.GLB.xp, _iz * ShadeConstants.GLB.zp))
                            .GetCombinedSuitableFrames(new Point2i(_ix, _iz), shadeFrame, offsetX, offsetZ, ShadeConstants.GLB.rX * 512)) harvested[_iz * ShadeConstants.GLB.rX + _ix] = true;
                    }
                }

                int x0 = ShadeConstants.GLB.nflowX(0, 0, ShadeConstants.GLB.rX) * 512;
                int z0 = ShadeConstants.GLB.nflowZ(0, 0, ShadeConstants.GLB.rZ) * 512;
                int SHADEX = ShadeConstants.GLB.rX * 512, SHADEZ = ShadeConstants.GLB.rZ * 512;
                for(int cz = 0; cz < 512; cz++) {
                    for(int cx = 0; cx < 512; cx++) {
                        int regionIndex = cz * 512 + cx;

                        int h = 319 - (-64 + heightMap[regionIndex]);
                        double x1 = (x0 + cx) + ShadeConstants.GLB.cosAcotgB * h, z1 = (z0 + cz) + -ShadeConstants.GLB.sinAcotgB * h;


                        if(heightMap[regionIndex] != 0) {
                            byte a = 3;
                            ChunkRenderer.SetShadeValuesLine(shadeFrame, shadeValues, ref a, regionIndex, SHADEX, SHADEZ, x1, z1);
                        }
                    }
                }

                RecalcIsShade();
                if(!shouldRedraw) {
                    CheckForDestruct();
                }
            }
        }
        public bool ShouldRedraw() => active && shouldRedraw;
        public void ResetAfterDraw() {
            lock(locker) {
                shouldRedraw = false;
                Array.Fill(shouldShade, false);

                CheckForDestruct();
            }
        }

        private void CheckForDestruct() {
            if(!harvested.Contains(false)) {
                Debug.WriteLine($"{tile.pos}'s shade f-ed");
                this.Destruct();
            }
        }


        private void RecalcIsShade(bool init = false) {
            for(int i = 0; i < shadeValues.Length / shadeStride; i++) {
                if(isShade[i]) continue;
                int j;
                for(j = 0; j < valuesLen[i]; j++) {
                    if(shadeValues[i * shadeStride + j] == false) break;
                }
                if(j == valuesLen[i]) {
                    if(!init) {
                        shouldShade[i] = true;
                        shouldRedraw = true;
                    }
                    isShade[i] = true;
                }
            }
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

        public void AddFrame(bool[] frame, Point2i dist) {
            bool[] harvested = new bool[ShadeConstants.GLB.rX * ShadeConstants.GLB.rZ];

            for(int i = 0; i < ShadeConstants.GLB.rX * ShadeConstants.GLB.rZ; i++) harvested[i] = true;

            for(int _ix = dist.X; _ix < ShadeConstants.GLB.rX; _ix++) {
                for(int _iz = dist.Z; _iz < ShadeConstants.GLB.rZ; _iz++) {
                    var p = pos + new Point2i(_ix * -ShadeConstants.GLB.xp, _iz * -ShadeConstants.GLB.zp);
                    harvested[_iz * ShadeConstants.GLB.rX + _ix] = (tileMap.GetTile(p) == null);
                }
            }
            harvested[dist.Z * ShadeConstants.GLB.rX + dist.X] = true;

            frames.TryAdd(dist, (frame, harvested));

            RemoveUnnecessary();
        }

        public bool GetCombinedSuitableFrames(Point2i d, bool[] shadeFrame, int offsetX, int offsetZ, int stride) {
            bool usedZeroZero = false;
            for(int xx = 0; xx <= d.X; xx++) {
                for(int zz = 0; zz <= d.Z; zz++) {
                    if(xx == d.X && zz == d.Z) continue;

                    var p = new Point2i(xx, zz);
                    if(frames.TryGetValue(p, out var fr)) {
                        for(int lx = 0; lx < 512; lx++) {
                            for(int lz = 0; lz < 512; lz++) {
                                shadeFrame[(offsetZ + lz) * stride + offsetX + lx] |= fr.frame[lz * 512 + lx];
                            }
                        }

                        fr.harvested[d.Z * ShadeConstants.GLB.rX + d.X] = true;

                        if(xx == 0 && zz == 0) usedZeroZero = true;
                    }
                    
                }
            }

            RemoveUnnecessary();
            return usedZeroZero;
        }

        private void RemoveUnnecessary() {
            var toRemove = frames.Where((p) => !p.Value.harvested.Contains(false)).Select(p => p.Key);
            foreach(var key in toRemove) {
                frames.TryRemove(key, out _);
            }
        }
    }
}
