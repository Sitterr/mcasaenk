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
using Mcasaenk.Shade3d;
using System.Net.Http.Headers;
using System.Xml.Linq;
using System.Collections.Concurrent;
using static Mcasaenk.Rendering.GenerateTilePool;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Documents;
using Accessibility;
using Mcasaenk.Nbt;
using CommunityToolkit.HighPerformance.Buffers;
using Mcasaenk.Rendering.ChunkRenderData;

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
        public GenerateTilePool pool { get => tile.GetOrigin().generateTilePool; }

        
        public void Generate() {
            if(Settings.SHADE3D) ShadeGenerate();
            else StandartGenerate();
        }

        public unsafe void StandartGenerate() {
            using var rawData = new RawData(pool);
            using var pixelBuffer = pool.BorrowPixels();

            //_ = new SortedList<int, int>().GetEnumerator;

            using(var regionReader = new McaReader(tile.GetOrigin().dimension.GetRegionPath(tile.pos))) {
                var ptrs = regionReader.ReadChunkOffsets();

                const int g = 10;
                Parallel.For(0, (1024 / g) + 1, new ParallelOptions { MaxDegreeOfParallelism = Settings.CHUNKRENDERMAXCONCURRENCY }, (j) => {                
                    for(int i = j * g; i < j * g + g; i++) {
                        if(i >= 1024) break;
                        int cz = i / 32, cx = i % 32;
                        var ptr = ptrs[i];
                        using var chunkdata = ChunkInterpreterStartingPoint.Read(tile, ptrs[i]);
                        if(chunkdata == null) continue;

                        if(chunkdata.ContainsHeightmaps() && Settings.USE_HEIGHTMAPS_GEN)
                            ChunkRenderer.ExtractWithHeightmaps(chunkdata, ColorMapping.Current, cx * 16, cz * 16, rawData, 319, -64);
                        else
                            ChunkRenderer.Extract(chunkdata, ColorMapping.Current, cx * 16, cz * 16, rawData, 319, -64);
                    }
                });
            }

            CreatePixels(pixelBuffer, new GenData(rawData, pool));

            img = GenerateBitmap(pixelBuffer);
        }

        public unsafe void ShadeGenerate() {
            using var rawData = new RawData(pool);
            using var pixelBuffer = pool.BorrowPixels();

            using(var regionReader = new McaReader(tile.GetOrigin().dimension.GetRegionPath(tile.pos))) {
                var ptrs = regionReader.ReadChunkOffsets();

                void doChunk(int cx, int cz) {
                    using var chunkdata = ChunkInterpreterStartingPoint.Read(tile, ptrs[cz * 32 + cx]);
                    ChunkRenderer.Extract3D(chunkdata, ColorMapping.Current, cx * 16, cz * 16, rawData, 319, -64);
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
                        if(tasks.Count >= Settings.CHUNKRENDERMAXCONCURRENCY) {
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
                        if(tasks.Count >= Settings.CHUNKRENDERMAXCONCURRENCY) {
                            Task.WaitAll(tasks.ToArray());
                            tasks.Clear();
                        }
                    }
                    Task.WaitAll(tasks.ToArray());
                    tasks.Clear();
                }
            }

            using var freeRaw = rawData.Free();
            var genData = new GenData(freeRaw, pool);

            
            { // save shades 
                tile.contgen.Safe(genData);

                tile.shade.Construct(freeRaw);

                // frame
                {
                    int i = 0;
                    foreach(var p in ShadeConstants.GLB.regionReach) {
                        int _iz = p.Z, _ix = p.X;

                        var frPos = tile.pos + new Point2i(_ix * ShadeConstants.GLB.xp, _iz * ShadeConstants.GLB.zp);

                        bool[] arr = null;
                        {
                            if(true) {
                                switch(ShadeConstants.GLB.regionDir[i]) {
                                    case ShadeConstants.RegionDir.c:
                                        break;

                                    case ShadeConstants.RegionDir.l:
                                        arr = tile.GetOrigin().GetTileShadeFrame(frPos).GetFrame(new Point2i(_ix + 1, _iz - 1));
                                        break;

                                    case ShadeConstants.RegionDir.r:
                                        arr = tile.GetOrigin().GetTileShadeFrame(frPos).GetFrame(new Point2i(_ix - 1, _iz + 1));
                                        break;
                                }
                            }
                            if(arr == null) arr = new bool[512 * 512];                
                        }

                        int offsetZ = ShadeConstants.GLB.nflowZ(_iz, 0, ShadeConstants.GLB.rZ) * 512;
                        int offsetX = ShadeConstants.GLB.nflowX(_ix, 0, ShadeConstants.GLB.rX) * 512;
                        for(int xx = offsetX; xx < offsetX + 512; xx++) {
                            for(int zz = offsetZ; zz < offsetZ + 512; zz++) {
                                int ai = (zz - offsetZ) * 512 + (xx - offsetX), si = zz * (ShadeConstants.GLB.rX * 512) + xx;
                                arr[ai] |= freeRaw.shadeFrame[si];
                            }
                        }
                        tile.GetOrigin().GetTileShadeFrame(frPos).AddFrame(arr, new Point2i(_ix, _iz));

                        i++;
                    }
                }

                // update tile shades that use the above frame
                {
                    var tileMap = tile.GetOrigin();

                    foreach(var p in ShadeConstants.GLB.regionReach) {
                        int _iz = p.Z, _ix = p.X;
                        //int iz = ShadeConstants.GLB.flowZ(_iz, 0, ShadeConstants.GLB.rZ), ix = ShadeConstants.GLB.flowX(_ix, 0, ShadeConstants.GLB.rX);

                        var t = tileMap.GetTile(this.tile.pos - new Point2i(_ix * ShadeConstants.GLB.xp, _iz * ShadeConstants.GLB.zp));

                        if(t != null) {
                            Array.Clear(freeRaw.shadeFrame);
                            t.shade.UpdateSelf(freeRaw.shadeFrame); // reuse
                        }
                    }
                }
            }         

            CreatePixels(pixelBuffer, genData);
            img = GenerateBitmap(pixelBuffer);

            tile.contgen.FinishedSafe();
        }



        public void CreatePixels(Span<uint> pixels, GenData genData) {
            for(int i = 0; i < 512 * 512; i++) {
                pixels[i] = ColorMapping.Current.GetColor(genData.blockIds[i], genData.biomeIds[i]);
            }

            if(Settings.WATER) {
                int i = 0;
                for(int z = 0; z < 512; z++) {
                    for(int x = 0; x < 512; x++, i++) {
                        if(genData.terrainHeights[i] != genData.heights[i]) {
                            float ratio = 0.5f + 0.5f / 40f * (float)((genData.heights[i]) - (genData.terrainHeights[i]));

                            ushort waterbiome = Settings.WATERBIOMES ? genData.biomeIds[i] : default;
                            pixels[i] = Global.Blend(ColorMapping.Current.GetColor(ColorMapping.BLOCK_WATER, waterbiome), pixels[i], ratio);
                        }
                    }
                }
            }

            if(Settings.STATIC_SHADE) {
                staticshade(pixels, genData.heights, ShadeConstants.GLB.cosA, ShadeConstants.GLB.sinA, Settings.STATIC_SHADE_POWER);
            } 


            if(Settings.SHADE3D) {
                int i = 0;
                for(int z = 0; z < 512; z++) {
                    for(int x = 0; x < 512; x++, i++) {
                        if(genData.isShade[i]) {
                            pixels[i] = Global.AddShade((uint)pixels[i], Settings.SHADE3DMOODYNESS, Settings.SHADE3DMOODYNESS, Settings.SHADE3DMOODYNESS);
                        }
                    }
                }
            }
        }



        public unsafe void Redraw() {
            if(tile.contgen.IsActive == false) return;
            var img = this.img.Clone();
            img.Lock();

            uint* pixels = (uint*)img.BackBuffer;
            CreatePixels(new Span<uint>(pixels, 512 * 512), tile.contgen.genData);
            tile.contgen.Redrawn();

            img.Unlock();
            img.Freeze();

            this.img = img;
        }
        private static WriteableBitmap GenerateBitmap(uint[] pixels) {
            WriteableBitmap output = new WriteableBitmap(512, 512, 96, 96, PixelFormats.Bgra32, null);

            if(pixels != null) {
                int stride = (int)output.Width * (output.Format.BitsPerPixel / 8);
                output.WritePixels(new Int32Rect(0, 0, 512, 512), pixels, stride, 0);
                output.Freeze();
            }
            
            
            return output;
        }






        private static void staticshade(Span<uint> pixelBuffer, ManArray<short> heights, double cosA, double sinA, float q) {
            cosA = Math.Round(cosA, 2);
            sinA = Math.Round(sinA, 2);

            int index = 0;
            for(int z = 0; z < 512; z++) {
                for(int x = 0; x < 512; x++, index++) {
                    float xShade, zShade;

                    if(pixelBuffer[index] == 0) {
                        continue;
                    }

                    {
                        if(z == 0) {
                            zShade = (heights[index + 512]) - (heights[index]);
                        } else if(z == 512 - 1) {
                            zShade = (heights[index]) - (heights[index - 512]);
                        } else {
                            zShade = ((heights[index + 512]) - (heights[index - 512])) * 2;
                        }

                        if(x == 0) {
                            xShade = (heights[index + 1]) - (heights[index]);
                        } else if(x == 512 - 1) {
                            xShade = (heights[index]) - (heights[index - 1]);
                        } else {
                            xShade = ((heights[index + 1]) - (heights[index - 1])) * 2;
                        }

                        double shade = -(cosA * xShade + -sinA * zShade);
                        if(shade < -8) {
                            shade = -8;
                        }
                        if(shade > 8) {
                            shade = 8;
                        }

                        int altitudeShade = 16 * (heights[index] - 64) / 255;
                        if(altitudeShade < -4) {
                            altitudeShade = -4;
                        }
                        if(altitudeShade > 24) {
                            altitudeShade = 24;
                        }
                        shade += altitudeShade;

                        pixelBuffer[index] = Global.AddShade((uint)pixelBuffer[index], (int)(shade * q), (int)(shade * q), (int)(shade * q));
                    }


                }
            }
        }
    }

}
