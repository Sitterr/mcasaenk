using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Xml.Linq;
using Mcasaenk.Shade3d;
using System.Windows.Shapes;
using Accessibility;
using System.Windows.Controls;
using Mcasaenk.Rendering.ChunkRenderData;
using System.Transactions;
using System.Windows.Media.Media3D;
using System.Reflection.Metadata;
using Mcasaenk.Colormaping;
using System.Windows.Documents;
using System.Windows.Media.Animation;
using System.Runtime.InteropServices;

namespace Mcasaenk.Rendering {
    public class ChunkRenderer {
        public static void Extract(IChunkInterpreter data, int x, int z, int y, RawData rdata) {
            if(data == null) return;
            if(data.ContainsInformation() == false) return;

            int maxh = Global.Settings.MAXABSHEIGHT;

            rdata.SetChunkScreenshotable(x / 16, z / 16, true);

            int x0 = ShadeConstants.GLB.nflowX(0, 0, ShadeConstants.GLB.rX) * 512;
            int z0 = ShadeConstants.GLB.nflowZ(0, 0, ShadeConstants.GLB.rZ) * 512;
            int SHADEX = ShadeConstants.GLB.rX * 512, SHADEZ = ShadeConstants.GLB.rZ * 512;

            for(int _cx = 0; _cx < 16; _cx++) {
                int cx = ShadeConstants.GLB.flowX(_cx, 0, 16);
                for(int _cz = 0; _cz < 16; _cz++) {
                    int cz = ShadeConstants.GLB.flowZ(_cz, 0, 16);
                    int regionIndex = (z + cz) * 512 + x + cx;
                    int xtotal = x0 + x + cx, ztotal = z0 + z + cz;


                    short airHeight = (short)y;
                    if(data.Colormap.AirHeightmapCompatible && Global.Settings.PREFERHEIGHTMAPS) {
                        short ah = HeightmapFilter.FilterAir(data, cx, cz, airHeight);
                        if(ah < y) airHeight = ah;
                    }
                    while(data.Colormap.FilterManager.GetBlockVal(data.GetBlock(cx, cz, airHeight)).ABSORBTION == 0 && airHeight >= 0) airHeight--;
                    if(airHeight == -1) {
                        rdata.SetChunkScreenshotable(x / 16, z / 16, false);
                        continue;
                    }
                    airHeight = (rdata.depthColumn is RawDataColumnColor) ?
                        (short)ReadColor((x, z), cx, cz, data, rdata, regionIndex, airHeight) :
                        (short)ReadId((x, z), cx, cz, data, rdata, regionIndex, airHeight);


                    if(rdata.shadeFrame != null) {
                        foreach(var col in rdata.columns) {
                            if(col.ContainsInfo(regionIndex) == false) continue;

                            int hs = maxh - col.heights[regionIndex];
                            double x1 = xtotal + ShadeConstants.GLB.cosAcotgB * hs, z1 = ztotal + -ShadeConstants.GLB.sinAcotgB * hs;
                            SetShadeValuesLine(rdata.shadeFrame, col.shadeValues, regionIndex, SHADEX, SHADEZ, (int)x1, (int)z1);
                        }
                        if(rdata.depthColumn.ContainsInfo(regionIndex)) {
                            int hs = maxh - (rdata.depthColumn.heights[regionIndex] + (rdata.depthColumn.depths != null ? rdata.depthColumn.depths[regionIndex] : 0));
                            double x1 = xtotal + ShadeConstants.GLB.cosAcotgB * hs, z1 = ztotal + -ShadeConstants.GLB.sinAcotgB * hs;
                            SetShadeValuesLine(rdata.shadeFrame, rdata.depthColumn.shadeValues, regionIndex, SHADEX, SHADEZ, (int)x1, (int)z1);
                        }

                        for(int h = airHeight; h >= 0; h--) {
                            var blid = data.GetBlock(cx, cz, h);
                            if(Global.Settings.NOSHADE_SHADE3D == false && data.Colormap.noShades.Contains(blid)) continue;
                            //if(blid == data.Colormap.BLOCK_AIR || blid == data.Colormap.depth) continue;
                            var filter = data.Colormap.FilterManager.GetBlockVal(blid);
                            if(filter.ABSORBTION == 0 || filter == data.Colormap.FilterManager.Depth) continue;

                            int hs = maxh - h;
                            double x1 = xtotal + ShadeConstants.GLB.cosAcotgB * hs, z1 = ztotal + -ShadeConstants.GLB.sinAcotgB * hs;
                            if(filter.ABSORBTION == 15 || Global.Settings.TRANSPARENTLAYERS <= 1) SetLine15(rdata.shadeFrame, SHADEX, SHADEZ, (int)x1, (int)z1);
                            else SetLine(rdata.shadeFrame, (byte)filter.ABSORBTION, SHADEX, SHADEZ, (int)x1, (int)z1);
                        }
                    }
                }
            }

        }


        
        static int ReadColor((int x, int z) c, int cx, int cz, IChunkInterpreter data, RawData rdata, int regionIndex, short origheight) {
            RawDataColumnColor[] columns = rdata.columns.Cast<RawDataColumnColor>().ToArray();
            RawDataColumnColor depthColumn = rdata.depthColumn as RawDataColumnColor;

            short height = origheight;
            if(columns.Length > 0) {
                ushort startblockid = data.GetBlock(cx, cz, height);
                uint startcolor = data.Colormap.BaseColor(startblockid);
                Tint starttint = data.Colormap.TintManager.GetBlockVal(startblockid);
                Filter startfilter = data.Colormap.FilterManager.GetBlockVal(startblockid);

                int coli = 0;
                float lcolor = startfilter.ABSORBTION / 15f;


                byte relvisostatuk = 255;
                short startheight = height, sth2 = height;
                byte startlight = Math.Max(data.GetBlockLight(cx, cz, startheight), data.GetBlockLight(cx, cz, startheight + 1));
                ushort startbiome = data.GetBiome(cx, cz, startheight);
                bool topblock = true;
                while(height-- > 0) {
                    ushort blockid = data.GetBlock(cx, cz, height);
                    uint color = data.Colormap.BaseColor(blockid);
                    Tint tint = data.Colormap.TintManager.GetBlockVal(blockid);
                    Filter filter = data.Colormap.FilterManager.GetBlockVal(blockid);
                    ushort biome = data.GetBiome(cx, cz, height);

                    if(filter.ABSORBTION == 0) {
                        sth2--;
                        continue;
                    }

                    if(filter != startfilter || tint != starttint || (biome != startbiome && startfilter != data.Colormap.FilterManager.Depth) || (startfilter.ABSORBTION == 15 && startfilter != data.Colormap.FilterManager.Depth)) {

                        if(startfilter == data.Colormap.FilterManager.Depth) {

                            if(startcolor <= 0x00FFFFFF || startfilter == data.Colormap.FilterManager.Error) rdata.SetChunkScreenshotable(c.x / 16, c.z / 16, false);
                            if(topblock && rdata.topblocks != null) {
                                rdata.topblocks[regionIndex] = blockid;
                                topblock = false;
                            }

                            depthColumn.Input(regionIndex,
                                color: tint.GetTintedColor(color, biome, height),
                                light: startlight,
                                biomeid: startbiome,
                                groupid: (byte)data.Colormap.Grouping.GetId(startfilter, starttint, !data.Colormap.noShades.Contains(startblockid)),
                                height: startheight,
                                depth: (short)(sth2 - height),
                                r_absortion: 15,
                                r_relvisost: ref relvisostatuk
                                );


                            coli = 1000;
                            break;
                        } else {
                            RawDataColumnColor col = coli++ < rdata.columns.Length ? columns[coli - 1] : depthColumn;

                            if(startcolor <= 0x00FFFFFF || startfilter == data.Colormap.FilterManager.Error) rdata.SetChunkScreenshotable(c.x / 16, c.z / 16, false);
                            if(topblock && rdata.topblocks != null) {
                                rdata.topblocks[regionIndex] = startblockid;
                                topblock = false;
                            }


                            col.Input(regionIndex,
                                color: startcolor,
                                light: startlight,
                                biomeid: startbiome,
                                groupid: (byte)data.Colormap.Grouping.GetId(startfilter, starttint, !data.Colormap.noShades.Contains(startblockid)),
                                height: startheight,
                                depth: col == depthColumn ? (short)0 : (short)(sth2 - height),
                                r_absortion: col == depthColumn ? 15 : startfilter.ABSORBTION,
                                r_relvisost: ref relvisostatuk
                                );

                        }
                        if(startfilter.ABSORBTION == 15) break;
                        if(coli >= rdata.columns.Length + 1) break;

                        sth2 = startheight = height;
                        startfilter = filter;
                        starttint = tint;
                        startbiome = biome;
                        lcolor = startfilter.ABSORBTION / 15f;
                        startcolor = color;
                        startblockid = blockid;
                        startlight = Math.Max(data.GetBlockLight(cx, cz, height), data.GetBlockLight(cx, cz, height + 1));
                    } else {
                        float q = startfilter.ABSORBTION / 15f * (1 - lcolor);
                        startcolor = Global.Blend(color, startcolor, q / lcolor);
                        lcolor += q;
                    }

                }

                if(height < 0 && startfilter == data.Colormap.FilterManager.Depth) {
                    depthColumn.Input(regionIndex,
                       color: 0,
                       light: startlight,
                       biomeid: startbiome,
                       groupid: (byte)data.Colormap.Grouping.GetId(startfilter, starttint, !data.Colormap.noShades.Contains(startblockid)),
                       height: startheight,
                       depth: (short)(startheight + 1),
                       r_absortion: 15,
                       r_relvisost: ref relvisostatuk
                       );

                    if(startcolor <= 0x00FFFFFF || startfilter == data.Colormap.FilterManager.Error) rdata.SetChunkScreenshotable(c.x / 16, c.z / 16, false);

                    coli++;
                }

                if(depthColumn.relvis != null) {
                    for(int w = 0; w < columns.Length; w++) {
                        columns[w].relvis[regionIndex] += (byte)((columns[w].relvis[regionIndex] / (float)(255 - relvisostatuk)) * relvisostatuk);
                    }
                    depthColumn.relvis[regionIndex] += (byte)((depthColumn.relvis[regionIndex] / (float)(255 - relvisostatuk)) * relvisostatuk);
                }

                if(coli == 0) {
                    rdata.SetChunkScreenshotable(c.x / 16, c.z / 16, false);
                }

                return origheight;
            } else {
                short waterHeight = height;
                if(rdata.depthColumn.depths != null) {
                    if(data.Colormap.WaterHeightmapCompatible && Global.Settings.PREFERHEIGHTMAPS) {
                        short wh = HeightmapFilter.FilterWater(data, cx, cz, waterHeight);
                        if(wh < waterHeight) waterHeight = wh;
                    }
                    while((data.GetBlock(cx, cz, waterHeight) == data.Colormap.depth || data.Colormap.FilterManager.GetBlockVal(data.GetBlock(cx, cz, waterHeight)).ABSORBTION == 0) && waterHeight >= 0) waterHeight--;
                }

                var heightfilter = data.Colormap.FilterManager.GetBlockVal(data.GetBlock(cx, cz, height));
                if(height != waterHeight && (heightfilter.ABSORBTION == 0 || heightfilter == data.Colormap.FilterManager.Depth)) {
                    ushort terrid = data.GetBlock(cx, cz, waterHeight);
                    uint terrcolor = data.Colormap.BaseColor(terrid);
                    Tint terrtint = data.Colormap.TintManager.GetBlockVal(terrid);
                    Tint wattint = data.Colormap.TintManager.GetBlockVal(data.GetBlock(cx, cz, height));

                    byte _r = 0;
                    depthColumn.Input(regionIndex, 
                        color: terrtint.GetTintedColor(terrcolor, data.GetBiome(cx, cz, waterHeight), waterHeight),
                        light: Math.Max(data.GetBlockLight(cx, cz, height), data.GetBlockLight(cx, cz, height + 1)),
                        biomeid: data.GetBiome(cx, cz, height),
                        groupid: (byte)data.Colormap.Grouping.GetId(data.Colormap.FilterManager.Depth, wattint, !data.Colormap.noShades.Contains(terrid)),
                        height: height,
                        depth: (short)(height - waterHeight),
                        r_absortion: 15,
                        r_relvisost: ref _r
                        );
                        
                    if(rdata.topblocks != null) rdata.topblocks[regionIndex] = terrid;

                    return waterHeight;

                } else {
                    ushort id = data.GetBlock(cx, cz, height);
                    Tint tint = data.Colormap.TintManager.GetBlockVal(id);
                    Filter filter = data.Colormap.FilterManager.GetBlockVal(id);

                    byte _r = 0;
                    depthColumn.Input(regionIndex,
                        color: data.Colormap.BaseColor(id),
                        light: Math.Max(data.GetBlockLight(cx, cz, height), data.GetBlockLight(cx, cz, height + 1)),
                        biomeid: data.GetBiome(cx, cz, height),
                        groupid: (byte)data.Colormap.Grouping.GetId(filter, tint, !data.Colormap.noShades.Contains(id)),
                        height: height,
                        depth: 0,
                        r_absortion: 15,
                        r_relvisost: ref _r
                        );


                    if(rdata.topblocks != null) rdata.topblocks[regionIndex] = id;
                    if(filter == data.Colormap.FilterManager.Error) rdata.SetChunkScreenshotable(c.x / 16, c.z / 16, false);

                    return height;
                }
            }
        }



        static int ReadId((int x, int z) c, int cx, int cz, IChunkInterpreter data, RawData rdata, int regionIndex, short origheight) {
            RawDataColumnId[] columns = rdata.columns.Cast<RawDataColumnId>().ToArray();
            RawDataColumnId depthColumn = rdata.depthColumn as RawDataColumnId;

            short height = origheight;
            if(columns.Length > 0) {
                int coli = 0;
                int depth = 1;
                byte relvisostatuk = 255;
                short startheight = height;
                ushort startblockid = data.GetBlock(cx, cz, height), startbiomeid = data.GetBiome(cx, cz, height);
                Filter startfilter = data.Colormap.FilterManager.GetBlockVal(startblockid);
                while(height-- >= 0) {
                    ushort blockid = data.GetBlock(cx, cz, height), biomeid = data.GetBiome(cx, cz, height);

                    Filter filter = data.Colormap.FilterManager.GetBlockVal(blockid);

                    if(filter.ABSORBTION == 0) continue;
                    if(startblockid != blockid || startbiomeid != biomeid || (startfilter.ABSORBTION == 15 && startfilter != data.Colormap.FilterManager.Depth)) {

                        if(startfilter == data.Colormap.FilterManager.Depth) {
                            depthColumn.Input(regionIndex,
                                blockid: blockid,
                                light: Math.Max(data.GetBlockLight(cx, cz, startheight), data.GetBlockLight(cx, cz, startheight + 1)),
                                biomeid: startbiomeid,
                                height: startheight,
                                depth: (short)depth,
                                r_absortion: data.Colormap.FilterManager.GetBlockVal(blockid).ABSORBTION,
                                r_relvisost: ref relvisostatuk
                                );

                            coli = 1000;
                            break;
                        } else {
                            RawDataColumnId col = coli++ < rdata.columns.Length ? columns[coli - 1] : depthColumn;

                            col.Input(regionIndex,
                                blockid: startblockid,
                                light: Math.Max(data.GetBlockLight(cx, cz, startheight), data.GetBlockLight(cx, cz, startheight + 1)),
                                biomeid: startbiomeid,
                                height: startheight,
                                depth: col == depthColumn ? (short)0 : (short)depth,
                                r_absortion: data.Colormap.FilterManager.GetBlockVal(startblockid).ABSORBTION,
                                r_relvisost: ref relvisostatuk);
                        }

                        if(startfilter.ABSORBTION == 15) break;
                        if(coli >= rdata.columns.Length + 1) break;

                        {
                            startblockid = blockid;
                            startbiomeid = biomeid;
                            startheight = height;
                            startfilter = filter;
                            depth = 1;
                        }
                    } else depth++;
                }


                if(height < 0 && startfilter == data.Colormap.FilterManager.Depth) {
                    depthColumn.Input(regionIndex,
                       blockid: Colormap.INVBLOCK,
                       light: Math.Max(data.GetBlockLight(cx, cz, startheight), data.GetBlockLight(cx, cz, startheight + 1)),
                       biomeid: startbiomeid,
                       height: startheight,
                       depth: (short)(startheight + 1),
                       r_absortion: 15,
                       r_relvisost: ref relvisostatuk
                       );

                    coli++;
                }

                if(depthColumn.relvis != null) {
                    for(int w = 0; w < columns.Length; w++) {
                        columns[w].relvis[regionIndex] += (byte)((columns[w].relvis[regionIndex] / (float)(255 - relvisostatuk)) * relvisostatuk);
                    }
                    depthColumn.relvis[regionIndex] += (byte)((depthColumn.relvis[regionIndex] / (float)(255 - relvisostatuk)) * relvisostatuk);
                }

                if(coli == 0) {
                    rdata.SetChunkScreenshotable(c.x / 16, c.z / 16, false);
                }

                return origheight;

            } else {
                short waterHeight = height;
                if(rdata.depthColumn.depths != null) {
                    if(data.Colormap.WaterHeightmapCompatible && Global.Settings.PREFERHEIGHTMAPS) {
                        short wh = HeightmapFilter.FilterWater(data, cx, cz, waterHeight);
                        if(wh < waterHeight) waterHeight = wh;
                    }
                    while((data.GetBlock(cx, cz, waterHeight) == data.Colormap.depth || data.Colormap.FilterManager.GetBlockVal(data.GetBlock(cx, cz, waterHeight)).ABSORBTION == 0) && waterHeight >= 0) waterHeight--;
                }

                if(waterHeight == -1) rdata.SetChunkScreenshotable(c.x / 16, c.z / 16, false);

                var heightfilter = data.Colormap.FilterManager.GetBlockVal(data.GetBlock(cx, cz, height));
                if(height != waterHeight && (heightfilter.ABSORBTION == 0 || heightfilter == data.Colormap.FilterManager.Depth)) {

                    byte _r = 0;
                    depthColumn.Input(regionIndex,
                        blockid: data.GetBlock(cx, cz, waterHeight),
                        light: Math.Max(data.GetBlockLight(cx, cz, height), data.GetBlockLight(cx, cz, height + 1)),
                        biomeid: data.GetBiome(cx, cz, height),
                        height: height,
                        depth: (short)(height - waterHeight),
                        r_absortion: 15,
                        r_relvisost: ref _r
                        );

                    return waterHeight;

                } else {
                    byte _r = 0;
                    depthColumn.Input(regionIndex,
                        blockid: data.GetBlock(cx, cz, height),
                        light: Math.Max(data.GetBlockLight(cx, cz, height), data.GetBlockLight(cx, cz, height + 1)),
                        biomeid: data.GetBiome(cx, cz, height),
                        height: height,
                        depth: 0,
                        r_absortion: 15,
                        r_relvisost: ref _r
                        );

                    return height;
                }
            }

        }













        static void SetLine(byte[] shadeFrame, byte value, int SHADEX, int SHADEZ, int x1, int z1) {
            if(Global.Settings.TRANSPARENTLAYERS <= 1) {
                SetLine15(shadeFrame, SHADEX, SHADEZ, x1, z1);
                return;
            }

            foreach(var r in ShadeConstants.GLB.blockReach) {
                int i = (z1 + r.p.Z) * SHADEX + (x1 + r.p.X);

                //if((z1 + p.p.Z) < 0 || (z1 + p.p.Z) >= SHADEZ) 
                //    continue;
                //if((x1 + p.p.X) < 0 || (x1 + p.p.X) >= SHADEX) 
                //    continue;

                switch(r.dir) {
                    case ShadeConstants.RegionDir.l:
                        ShadeConstants.SetLeft(shadeFrame, i, ShadeConstants.CombineShades(ShadeConstants.GetLeft(shadeFrame, i), value));
                        break;

                    case ShadeConstants.RegionDir.r:
                        ShadeConstants.SetRight(shadeFrame, i, ShadeConstants.CombineShades(ShadeConstants.GetRight(shadeFrame, i), value));
                        break;

                    case ShadeConstants.RegionDir.c:
                        var valleft = ShadeConstants.CombineShades(ShadeConstants.GetLeft(shadeFrame, i), value);
                        var valright = ShadeConstants.CombineShades(ShadeConstants.GetRight(shadeFrame, i), value);
                        ShadeConstants.SetBoth(shadeFrame, i, valleft, valright);
                        break;
                }
            }
        }
        static void SetLine15(byte[] shadeFrame, int SHADEX, int SHADEZ, int x1, int z1) {
            foreach(var r in ShadeConstants.GLB.blockReach) {
                int i = (z1 + r.p.Z) * SHADEX + (x1 + r.p.X);

                //if((z1 + p.p.Z) < 0 || (z1 + p.p.Z) >= SHADEZ) 
                //    continue;
                //if((x1 + p.p.X) < 0 || (x1 + p.p.X) >= SHADEX) 
                //    continue;

                switch(r.dir) {
                    case ShadeConstants.RegionDir.l:
                    //ShadeConstants.SetLeft(shadeFrame, i, 15);
                    //break;
                    case ShadeConstants.RegionDir.r:
                    //ShadeConstants.SetRight(shadeFrame, i, 15);
                    //break;

                    case ShadeConstants.RegionDir.c:
                        shadeFrame[i] = 255;
                        break;
                }
            }
        }


        public static void SetShadeValuesLine(byte[] shadeFrame, byte[] shades, int regionIndex, int SHADEX, int SHADEZ, int x1, int z1) {
            if(Global.Settings.TRANSPARENTLAYERS <= 1) {
                SetShadeValuesLine15(shadeFrame, shades, regionIndex, SHADEX, SHADEZ, x1, z1);
                return;
            }

            var blockReach = ShadeConstants.GLB.blockReach;
            for(int i = 0; i < blockReach.Length; i++) {
                var p = blockReach[i].p;

                //if(((z1 + p.Z) < 0 || (z1 + p.Z) >= SHADEZ) || ((x1 + p.X) < 0 || (x1 + p.X) >= SHADEX)) {
                //ShadeConstants.SetS(shades, regionIndex * ShadeConstants.GLB.blockReachLenMax + i, 0);
                //    continue;
                //}

                if(blockReach[i].dir == ShadeConstants.RegionDir.l || blockReach[i].dir == ShadeConstants.RegionDir.c)
                    ShadeConstants.SetLeft(shades, regionIndex * ShadeConstants.GLB.blockReachLenMax + i,
                        ShadeConstants.CombineShades(ShadeConstants.GetLeft(shades, regionIndex * ShadeConstants.GLB.blockReachLenMax + i), ShadeConstants.GetLeft(shadeFrame, (z1 + p.Z) * SHADEX + (x1 + p.X)))
                        );

                if(blockReach[i].dir == ShadeConstants.RegionDir.r || blockReach[i].dir == ShadeConstants.RegionDir.c)
                    ShadeConstants.SetRight(shades, regionIndex * ShadeConstants.GLB.blockReachLenMax + i,
                        ShadeConstants.CombineShades(ShadeConstants.GetRight(shades, regionIndex * ShadeConstants.GLB.blockReachLenMax + i), ShadeConstants.GetRight(shadeFrame, (z1 + p.Z) * SHADEX + (x1 + p.X)))
                        );
            }
        }
        public static void SetShadeValuesLine15(byte[] shadeFrame, byte[] shades, int regionIndex, int SHADEX, int SHADEZ, int x1, int z1) {
            var blockReach = ShadeConstants.GLB.blockReach;
            for(int i = 0; i < blockReach.Length; i++) {
                var p = blockReach[i].p;

                //if(((z1 + p.Z) < 0 || (z1 + p.Z) >= SHADEZ) || ((x1 + p.X) < 0 || (x1 + p.X) >= SHADEX)) {
                //ShadeConstants.SetS(shades, regionIndex * ShadeConstants.GLB.blockReachLenMax + i, 0);
                //    continue;
                //}

                byte val = shadeFrame[(z1 + p.Z) * SHADEX + (x1 + p.X)];
                shades[regionIndex * ShadeConstants.GLB.blockReachLenMax + i] |= val;
            }
        }

    }

}
