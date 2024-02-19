using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using System.Diagnostics;
using Mcasaenk.UI.Canvas;
using System.IO;
using static System.Net.WebRequestMethods;
using Mcasaenk.Shade3d;
using System.Net.Http.Headers;
using System.Xml.Linq;
using System.Collections.Concurrent;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Mcasaenk.Rendering
{
    public class TileImage {
        private readonly Tile tile;
        private WriteableBitmap img;
        public TileImage(Tile tile) {
            this.tile = tile;
        }
        public ImageSource GetImage() {
            return img;
        }

        public static long lastRedrawTime = 0;
        
        public unsafe void StandartGenerate(StandardGenerateTilePool pool) {
            int[] pixelBuffer = pool.pixelBuffer.Rent(512 * 512);
            int[] waterPixels = pool.waterPixels.Rent(512 * 512);
            short[] terrainHeights = pool.terrainHeights.Rent(512 * 512);
            short[] waterHeights = pool.waterHeights.Rent(512 * 512);

            using(var regionReader = new RegionReader(tile.GetOrigin().dimension.GetRegionPath(tile.pos))) {
                var ptrs = regionReader.ReadChunkOffsets();

                const int g = 5;
                Parallel.For(0, 1024 / g, new ParallelOptions { MaxDegreeOfParallelism = pool.parallelChunksPerRegion }, (j) => {
                    for(int i = j * g; i < j * g + g; i++) {
                        int cz = i / 32, cx = i % 32;
                        using var chunk = RegionReader.LazyRenderData(pool, ptrs[i]);
                        ChunkRenderer.DrawChunk(chunk, ColorMapping.Current, cx * 16, cz * 16, pixelBuffer, waterPixels, terrainHeights, waterHeights, 319, -64);
                    }
                });
            }

            staticshade(pixelBuffer, waterPixels, terrainHeights, waterHeights, ShadeConstants.GLB.cosA, ShadeConstants.GLB.sinA);

            img = GenerateBitmap(pixelBuffer);

            pool.pixelBuffer.Return(pixelBuffer, true);
            pool.waterPixels.Return(waterPixels, true);
            pool.terrainHeights.Return(terrainHeights, true);
            pool.waterHeights.Return(waterHeights, true);
        }

        public unsafe void ShadeGenerate(ShadeGenerateTilePool pool) {
            int[] pixelBuffer = pool.pixelBuffer.Rent(512 * 512);
            int[] waterPixels = pool.waterPixels.Rent(512 * 512);
            short[] terrainHeights = pool.terrainHeights.Rent(512 * 512);
            short[] waterHeights = new short[512 * 512]; // !

            bool[] shadeFrame = pool.shadeFrame.Rent((ShadeConstants.GLB.rX * 512) * (ShadeConstants.GLB.rZ * 512));
            bool[] shadeValues = new bool[512 * 512 * ShadeConstants.GLB.blockReachLenMax]; // !
            byte[] shadeValuesLen = new byte[512 * 512]; // !

            using(var regionReader = new RegionReader(tile.GetOrigin().dimension.GetRegionPath(tile.pos))) {
                var ptrs = regionReader.ReadChunkOffsets();

                void doChunk(int cx, int cz) {
                    using var chunk = RegionReader.LazyRenderData(pool, ptrs[cz * 32 + cx]);
                    ChunkRenderer.DrawChunk3D(chunk, ColorMapping.Current, cx * 16, cz * 16, pixelBuffer, waterPixels, terrainHeights, waterHeights, shadeFrame, shadeValues, shadeValuesLen, 319, -64);
                }

                const int g = 5;
                List<Task> tasks = new List<Task>();
                for(int i = 0; i < 32; i++) {
                    for(int _c = 0; _c <= i; _c += g) {
                        int c = _c;
                        var task = Task.Run(() => { 
                            for(int cc = c; cc < c + g; cc++) {
                                if(cc > i) break;

                                int _cx = cc, _cz = i - cc;
                                int cx = ShadeConstants.GLB.flowX(_cx, 0, 32), cz = ShadeConstants.GLB.flowZ(_cz, 0, 32);
                                doChunk(cx, cz);
                            }
                        });
                        tasks.Add(task);
                        if(tasks.Count >= pool.parallelChunksPerRegion) {
                            Task.WaitAll(tasks.ToArray());
                            tasks.Clear();
                        }
                    }
                    Task.WaitAll(tasks.ToArray());
                    tasks.Clear();

                }
                for(int i = 1; i < 32; i++) {
                    for(int _c = i; _c < 32; _c += g) {
                        int c = _c;
                        var task = Task.Run(() => {
                            for(int cc = c; cc < c + g; cc++) {
                                if(cc >= 32) break;

                                int _cx = cc, _cz = 32 - cc + i - 1;
                                int cx = ShadeConstants.GLB.flowX(_cx, 0, 32), cz = ShadeConstants.GLB.flowZ(_cz, 0, 32);
                                doChunk(cx, cz);

                            }
                        });
                        tasks.Add(task);
                        if(tasks.Count >= pool.parallelChunksPerRegion) {
                            Task.WaitAll(tasks.ToArray());
                            tasks.Clear();
                        }
                    }
                    Task.WaitAll(tasks.ToArray());
                    tasks.Clear();
                }


                {
                    //for(int _cz = 0; _cz < 32; _cz++) {
                    //    int cz = ShadeConstants.GLOBAL.flowZ(_cz, 0, 32);
                    //    for(int _cx = 0; _cx < 32; _cx++) {
                    //        int cx = ShadeConstants.GLOBAL.flowX(_cx, 0, 32);
                    //        doChunk(cx, cz);
                    //    }
                    //}
                }
            }

            staticshade(pixelBuffer, waterPixels, terrainHeights, waterHeights, ShadeConstants.GLB.cosA, ShadeConstants.GLB.sinA);
            img = GenerateBitmap(pixelBuffer);


            { // save shades 
                tile.shade.Construct(waterHeights, shadeValues, shadeValuesLen);

                // frame
                {
                    for(int _ix = 0; _ix < ShadeConstants.GLB.rX; _ix++) {                  
                        for(int _iz = 0; _iz < ShadeConstants.GLB.rZ; _iz++) {
                            int offsetZ = ShadeConstants.GLB.nflowZ(_iz, 0, ShadeConstants.GLB.rZ) * 512;
                            int offsetX = ShadeConstants.GLB.nflowX(_ix, 0, ShadeConstants.GLB.rX) * 512;

                            bool[] arr = new bool[512 * 512];
                            for(int xx = offsetX; xx < offsetX + 512; xx++) {
                                for(int zz = offsetZ; zz < offsetZ + 512; zz++) {
                                    int ai = (zz - offsetZ) * 512 + (xx - offsetX), si = zz * (ShadeConstants.GLB.rX * 512) + xx;
                                    arr[ai] = shadeFrame[si];
                                }
                            }
                            tile.GetOrigin().GetTileShadeFrame(tile.pos + new Point2i(_ix * ShadeConstants.GLB.xp, _iz * ShadeConstants.GLB.zp)).AddFrame(arr, new Point2i(_ix, _iz));
                        }
                    }
                }

                // update
                {
                    var tileMap = tile.GetOrigin();
                    for(int _ix = 0; _ix < ShadeConstants.GLB.rX; _ix++) {
                        int ix = ShadeConstants.GLB.flowX(_ix, 0, ShadeConstants.GLB.rX);
                        for(int _iz = 0; _iz < ShadeConstants.GLB.rZ; _iz++) {
                            int iz = ShadeConstants.GLB.flowZ(_iz, 0, ShadeConstants.GLB.rZ);

                            var t = tileMap.GetTile(this.tile.pos + new Point2i(ix, iz));

                            if(t != null) {
                                Array.Clear(shadeFrame);
                                t.shade.UpdateInfo(shadeFrame); // reuse
                            }
                        }
                    }
                }
            }

            pool.pixelBuffer.Return(pixelBuffer, true);
            pool.waterPixels.Return(waterPixels, true);
            pool.terrainHeights.Return(terrainHeights, true);
            pool.shadeFrame.Return(shadeFrame, true);
        }


        public unsafe void ShadeRedraw() {
            var img = this.img.Clone();
            img.Lock();

            int* pixels = (int*)img.BackBuffer;

            bool[] shouldShade = tile.shade.shouldShade;
            for(int z = 0; z < 512; z++) {
                for(int x = 0; x < 512; x++) {
                    int i = z * 512 + x;
                    if(shouldShade[i]) {
                        pixels[i] = (int)Global.AddShade((uint)pixels[i], Settings.SHADE3DMOODYNESS, Settings.SHADE3DMOODYNESS, Settings.SHADE3DMOODYNESS);
                    }
                }
            }
            tile.shade.ResetAfterDraw();

            img.Unlock();
            img.Freeze();

            this.img = img;
        }




        private static void staticshade(int[] pixelBuffer, int[] waterPixels, short[] terrainHeights, short[] waterHeights, double cosA, double sinA) {
            cosA = Math.Round(cosA, 2);
            sinA = Math.Round(sinA, 2);

            int index = 0;
            for(int z = 0; z < 512; z++) {
                for(int x = 0; x < 512; x++, index++) {
                    float xShade, zShade;

                    if(pixelBuffer[index] == 0) {
                        continue;
                    }

                    if(terrainHeights[index] != waterHeights[index]) {
                        float ratio = 0.5f - 0.5f / 40f * (float)((waterHeights[index]) - (terrainHeights[index]));
                        pixelBuffer[index] = (int)Global.Blend((uint)pixelBuffer[index], (uint)waterPixels[index], ratio);
                    } else {
                        if(z == 0) {
                            zShade = (waterHeights[index + 512]) - (waterHeights[index]);
                        } else if(z == 512 - 1) {
                            zShade = (waterHeights[index]) - (waterHeights[index - 512]);
                        } else {
                            zShade = ((waterHeights[index + 512]) - (waterHeights[index - 512])) * 2;
                        }

                        if(x == 0) {
                            xShade = (waterHeights[index + 1]) - (waterHeights[index]);
                        } else if(x == 512 - 1) {
                            xShade = (waterHeights[index]) - (waterHeights[index - 1]);
                        } else {
                            xShade = ((waterHeights[index + 1]) - (waterHeights[index - 1])) * 2;
                        }

                        double shade = -(cosA * xShade + -sinA * zShade);
                        if(shade < -8) {
                            shade = -8;
                        }
                        if(shade > 8) {
                            shade = 8;
                        }

                        int altitudeShade = 16 * (waterHeights[index] - 64) / 255;
                        if(altitudeShade < -4) {
                            altitudeShade = -4;
                        }
                        if(altitudeShade > 24) {
                            altitudeShade = 24;
                        }
                        shade += altitudeShade;

                        if(Settings.SHADE3D) {
                            pixelBuffer[index] = (int)Global.AddShade((uint)pixelBuffer[index], (int)(shade * 3), (int)(shade * 3), (int)(shade * 3));
                        } else {
                            pixelBuffer[index] = (int)Global.AddShade((uint)pixelBuffer[index], (int)(shade * 8), (int)(shade * 8), (int)(shade * 8));
                        }

                        

                    }
                }
            }
        }


        private static WriteableBitmap GenerateBitmap(int[] pixels) {
            WriteableBitmap output = new WriteableBitmap(512, 512, 96, 96, PixelFormats.Bgra32, null);

            int stride = (int)output.Width * (output.Format.BitsPerPixel / 8);
            output.WritePixels(new Int32Rect(0, 0, 512, 512), pixels, stride, 0);

            output.Freeze();
            return output;
        }
    }

}
