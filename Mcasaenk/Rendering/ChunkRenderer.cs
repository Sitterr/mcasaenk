using Mcasaenk.Colormaping;
using Mcasaenk.Rendering.ChunkRenderData;
using Mcasaenk.Shade3d;

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
                    airHeight = (short)ReadId((x, z), cx, cz, data, rdata, regionIndex, airHeight);


                    if(rdata.shadeFrame != null) {
                        foreach(var col in rdata.columns) {
                            if(col.ContainsInfo(regionIndex) == false) continue;

                            int hs = maxh - col.heights[regionIndex];
                            double x1 = xtotal + ShadeConstants.GLB.cosAcotgB * hs, z1 = ztotal + -ShadeConstants.GLB.sinAcotgB * hs;
                            SetShadeValuesLine(rdata.shadeFrame, col.shadeValues, regionIndex, SHADEX, SHADEZ, (int)x1, (int)z1);
                        }
                        if(rdata.depthColumn.ContainsInfo(regionIndex)) {
                            int hs = maxh - (rdata.depthColumn.heights[regionIndex] + (rdata.depthColumn.depths15_lightfrombottom1 != null ? rdata.depthColumn.depths15_lightfrombottom1[regionIndex] >> 1 : 0));
                            double x1 = xtotal + ShadeConstants.GLB.cosAcotgB * hs, z1 = ztotal + -ShadeConstants.GLB.sinAcotgB * hs;
                            SetShadeValuesLine(rdata.shadeFrame, rdata.depthColumn.shadeValues, regionIndex, SHADEX, SHADEZ, (int)x1, (int)z1);
                        }

                        for(int h = airHeight; h >= 0; h--) {
                            var blid = data.GetBlock(cx, cz, h);
                            if(Global.Settings.NOSHADE_SHADE3D == false && data.Colormap.BlocksManager.noShades.Contains(blid)) continue;
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


        static int ReadId((int x, int z) c, int cx, int cz, IChunkInterpreter data, RawData rdata, int regionIndex, short origheight) {
            RawDataColumn[] columns = rdata.columns.Cast<RawDataColumn>().ToArray();
            RawDataColumn depthColumn = rdata.depthColumn as RawDataColumn;

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
                                lightfrombottom: data.GetBlockLight(cx, cz, startheight - 1) > data.GetBlockLight(cx, cz, startheight + 1),
                                biomeid: startbiomeid,
                                height: startheight,
                                depth: (short)depth,
                                r_absortion: data.Colormap.FilterManager.GetBlockVal(blockid).ABSORBTION,
                                r_relvisost: ref relvisostatuk
                                );

                            coli = 1000;
                            break;
                        } else {
                            RawDataColumn col = coli++ < rdata.columns.Length ? columns[coli - 1] : depthColumn;

                            col.Input(regionIndex,
                                blockid: startblockid,
                                light: Math.Max(data.GetBlockLight(cx, cz, startheight), data.GetBlockLight(cx, cz, startheight + 1)),
                                lightfrombottom: data.GetBlockLight(cx, cz, startheight - 1) > data.GetBlockLight(cx, cz, startheight + 1),
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
                            startheight = (short)(height + 1);
                            startfilter = filter;
                            depth = 1;
                        }
                    } else depth++;
                }


                if(height < 0 && startfilter == data.Colormap.FilterManager.Depth) {
                    depthColumn.Input(regionIndex,
                       blockid: Colormap.INVBLOCK,
                       light: Math.Max(data.GetBlockLight(cx, cz, startheight), data.GetBlockLight(cx, cz, startheight + 1)),
                       lightfrombottom: data.GetBlockLight(cx, cz, startheight - 1) > data.GetBlockLight(cx, cz, startheight + 1),
                       biomeid: startbiomeid,
                       height: startheight,
                       depth: (short)(startheight + 1),
                       r_absortion: 15,
                       r_relvisost: ref relvisostatuk
                       );

                    coli++;
                }

                if(coli == 0) {
                    rdata.SetChunkScreenshotable(c.x / 16, c.z / 16, false);
                }

                return origheight;

            } else {
                short waterHeight = height;
                if(rdata.depthColumn.depths15_lightfrombottom1 != null) {
                    if(data.Colormap.WaterHeightmapCompatible && Global.Settings.PREFERHEIGHTMAPS) {
                        short wh = HeightmapFilter.FilterWater(data, cx, cz, waterHeight);
                        if(wh < waterHeight) waterHeight = wh;
                    }
                    while((data.GetBlock(cx, cz, waterHeight) == BlockManager.depth || data.Colormap.FilterManager.GetBlockVal(data.GetBlock(cx, cz, waterHeight)).ABSORBTION == 0) && waterHeight >= 0) waterHeight--;
                }

                if(waterHeight == -1) rdata.SetChunkScreenshotable(c.x / 16, c.z / 16, false);

                var heightfilter = data.Colormap.FilterManager.GetBlockVal(data.GetBlock(cx, cz, height));
                if(height != waterHeight && (heightfilter.ABSORBTION == 0 || heightfilter == data.Colormap.FilterManager.Depth)) {

                    byte _r = 0;
                    depthColumn.Input(regionIndex,
                        blockid: data.GetBlock(cx, cz, waterHeight),
                        light: Math.Max(data.GetBlockLight(cx, cz, height), data.GetBlockLight(cx, cz, height + 1)),
                        lightfrombottom: data.GetBlockLight(cx, cz, height - 1) > data.GetBlockLight(cx, cz, height + 1),
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
                        lightfrombottom: data.GetBlockLight(cx, cz, height - 1) > data.GetBlockLight(cx, cz, height + 1),
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
