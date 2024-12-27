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
using System.Buffers;

namespace Mcasaenk.Rendering
{
    public class TileGenerate {
        public static unsafe GenData StandartGenerate(Tile tile) {
            if(File.Exists(tile.GetOrigin().dimension.GetRegionPath(tile.pos)) == false) return null;

            var rawData = new RawData();
            using(var regionReader = new UnmanagedMcaReader(tile.GetOrigin().dimension.GetRegionPath(tile.pos))) {
                var streams = regionReader.ReadChunkOffsets();

                for(int i = 0; i < 1024; i++) {
                    int cz = i / 32, cx = i % 32;
                    using var chunkdata = ChunkInterpreterStartingPoint.Read(streams[i]);
                    if(chunkdata == null) continue;

                    ChunkRenderer.Extract(chunkdata, cx * 16, cz * 16, Global.Settings.ABSY, rawData);
                }
            }

            var genData = new GenData(rawData, Global.App.Colormap.depth);
            return genData;
        }

        public static unsafe GenData ShadeGenerate(Tile tile) {
            if(File.Exists(tile.GetOrigin().dimension.GetRegionPath(tile.pos)) == false) return null;

            var rawData = new RawData();
            using(var regionReader = new UnmanagedMcaReader(tile.GetOrigin().dimension.GetRegionPath(tile.pos))) {
                var streams = regionReader.ReadChunkOffsets();

                void doChunk(int cx, int cz) {
                    using var chunkdata = ChunkInterpreterStartingPoint.Read(streams[cz * 32 + cx]);
                    if(chunkdata == null) return;
                    ChunkRenderer.Extract(chunkdata, cx * 16, cz * 16, Global.Settings.ABSY, rawData);
                }


                for(int i = 0; i < 32; i++) {
                    for(int _c = 0; _c <= i; _c += 1) {
                        int _cx = _c, _cz = i - _c;
                        int cx = ShadeConstants.GLB.flowX(_cx, 0, 32), cz = ShadeConstants.GLB.flowZ(_cz, 0, 32);
                        doChunk(cx, cz);
                    }

                }
                for(int i = 1; i < 32; i++) {
                    for(int _c = i; _c < 32; _c += 1) {
                        int _cx = _c, _cz = 32 - _c + i - 1;
                        int cx = ShadeConstants.GLB.flowX(_cx, 0, 32), cz = ShadeConstants.GLB.flowZ(_cz, 0, 32);
                        doChunk(cx, cz);
                    }
                }
            }

            var genData = new GenData(rawData, Global.App.Colormap.depth);
            
            { // save shades 
                tile.shade.Construct(rawData, genData);

                // frame
                {
                    foreach(var p in ShadeConstants.GLB.regionReach) {
                        int _iz = p.p.Z, _ix = p.p.X;

                        var frPos = tile.pos + new Point2i(_ix * ShadeConstants.GLB.xp, _iz * ShadeConstants.GLB.zp);

                        byte[] arr = null;
                        {
                            if(true) {
                                switch(p.dir) {
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
                            if(arr == null) arr = new byte[512 * 512];                
                        }

                        int offsetZ = ShadeConstants.GLB.nflowZ(_iz, 0, ShadeConstants.GLB.rZ) * 512;
                        int offsetX = ShadeConstants.GLB.nflowX(_ix, 0, ShadeConstants.GLB.rX) * 512;
                        for(int xx = offsetX; xx < offsetX + 512; xx++) {
                            for(int zz = offsetZ; zz < offsetZ + 512; zz++) {
                                int ai = (zz - offsetZ) * 512 + (xx - offsetX), si = zz * (ShadeConstants.GLB.rX * 512) + xx;

                                byte left = ShadeConstants.CombineShades(ShadeConstants.GetLeft(arr, ai), ShadeConstants.GetLeft(rawData.shadeFrame, si)),
                                    right = ShadeConstants.CombineShades(ShadeConstants.GetRight(arr, ai), ShadeConstants.GetRight(rawData.shadeFrame, si));

                                ShadeConstants.SetBoth(arr, ai, left, right);
                            }
                        }
                        tile.GetOrigin().GetTileShadeFrame(frPos).AddFrame(arr, new Point2i(_ix, _iz));
                    }
                }

                // update tile shades that use the above frame
                {
                    var tileMap = tile.GetOrigin();

                    foreach(var p in ShadeConstants.GLB.regionReach) {
                        int _iz = p.p.Z, _ix = p.p.X;
                        //int iz = ShadeConstants.GLB.flowZ(_iz, 0, ShadeConstants.GLB.rZ), ix = ShadeConstants.GLB.flowX(_ix, 0, ShadeConstants.GLB.rX);

                        var t = tileMap.GetTile(tile.pos - new Point2i(_ix * ShadeConstants.GLB.xp, _iz * ShadeConstants.GLB.zp));

                        if(t != null) {
                            Array.Clear(rawData.shadeFrame);
                            t.shade.UpdateSelf(rawData.shadeFrame); // reuse
                        }
                    }
                }
            }

            return genData;
        }
    }

}
