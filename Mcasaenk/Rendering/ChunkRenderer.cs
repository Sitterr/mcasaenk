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

            int maxh = Global.Settings.MAXABSHEIGHT;
            if(data == null) return;
            if(data.ContainsInformation() == false) return;

            int x0 = ShadeConstants.GLB.nflowX(0, 0, ShadeConstants.GLB.rX) * 512;
            int z0 = ShadeConstants.GLB.nflowZ(0, 0, ShadeConstants.GLB.rZ) * 512;
            int SHADEX = ShadeConstants.GLB.rX * 512, SHADEZ = ShadeConstants.GLB.rZ * 512;

            for(int _cx = 0; _cx < 16; _cx++) {
                int cx = ShadeConstants.GLB.flowX(_cx, 0, 16);
                for(int _cz = 0; _cz < 16; _cz++) {
                    int cz = ShadeConstants.GLB.flowZ(_cz, 0, 16);
                    int regionIndex = (z + cz) * 512 + x + cx;

                    int xtotal = x0 + x + cx, ztotal = z0 + z + cz;




                    //short airHeight = RFilter.AIR_FILTER(data, y, maxh - 1)(data, cx, cz, (short)y);
                    short airHeight = (short)y;
                    if(data.Colormap.AirHeightmapCompatible && Global.Settings.PREFERHEIGHTMAPS) {
                        short ah = HeightmapFilter.FilterAir(data, cx, cz, airHeight);
                        if(ah < y) airHeight = ah;
                    }
                    while(data.Colormap.FilterManager.GetBlockVal(data.GetBlock(cx, cz, airHeight)).ABSORBTION == 0 && airHeight > 0) airHeight--;
                    short height = airHeight, waterHeight = airHeight;

                    if(rdata.columns.Length > 0) {
                        ushort startblockid = data.GetBlock(cx, cz, height);
                        uint startcolor = data.Colormap.BaseColor(startblockid);
                        Tint starttint = data.Colormap.TintManager.GetBlockVal(startblockid);
                        Filter startfilter = data.Colormap.FilterManager.GetBlockVal(startblockid);

                        int coli = 0;
                        float lcolor = startfilter.ABSORBTION / 15f;
                            
                        if(height < 0) continue;

                        short startheight = height;
                        byte startlight = Math.Max(data.GetBlockLight(cx, cz, startheight), data.GetBlockLight(cx, cz, startheight + 1));
                        ushort startbiome = data.GetBiome(cx, cz, startheight);
                        height--;

                        while(height >= 0) {
                            ushort blockid = data.GetBlock(cx, cz, height);
                            uint color = data.Colormap.BaseColor(blockid);
                            Tint tint = data.Colormap.TintManager.GetBlockVal(blockid);
                            Filter filter = data.Colormap.FilterManager.GetBlockVal(blockid);

                            if(filter.ABSORBTION == 0 && false) {
                                height--;
                                continue;
                            }
                               

                            ushort biome = data.GetBiome(cx, cz, height);

                            if(filter != startfilter || tint != starttint || biome != startbiome || (startfilter.ABSORBTION == 15 && startfilter != data.Colormap.FilterManager.Depth)) {

                                if(startfilter.ABSORBTION > 0) {

                                    if(startfilter == data.Colormap.FilterManager.Depth) {
                                        rdata.depthColumn.heights[regionIndex] = startheight;
                                        rdata.depthColumn.depths[regionIndex] = (short)(startheight - height);

                                        rdata.depthColumn.biomeIds10_groupIds6[regionIndex] = RawDataColumn.BiomeGroupMaker(startbiome, (byte)data.Colormap.Grouping.GetId(startfilter, starttint, !data.Colormap.noShades.Contains(startblockid)));
                                        rdata.depthColumn.color24_light4_none4[regionIndex] = RawDataColumn.ColorLightMaker(tint.GetTintedColor(color, biome, height), startlight);

                                        break;
                                    } else {
                                        RawDataColumn col;
                                        if(coli == rdata.columns.Length) col = rdata.depthColumn;
                                        else col = rdata.columns[coli];


                                        col.biomeIds10_groupIds6[regionIndex] = RawDataColumn.BiomeGroupMaker(startbiome, (byte)data.Colormap.Grouping.GetId(startfilter, starttint, !data.Colormap.noShades.Contains(startblockid)));
                                        col.heights[regionIndex] = startheight;
                                        if(col != rdata.depthColumn) col.depths[regionIndex] = (short)(startheight - height);
                                        col.color24_light4_none4[regionIndex] = RawDataColumn.ColorLightMaker(startcolor, startlight);
                                        coli++;
                                    }

                                }
                                if(startfilter.ABSORBTION == 15) break;

                                startheight = height;
                                startfilter = filter;
                                starttint = tint;
                                startbiome = biome;
                                lcolor = startfilter.ABSORBTION / 15f;
                                startcolor = color;
                                startblockid = blockid;
                                if(coli >= rdata.columns.Length + 1) break;
                            } else {
                                float q = startfilter.ABSORBTION / 15f * (1 - lcolor);
                                startcolor = Global.Blend(color, startcolor, q / lcolor);
                                lcolor += q;
                            }

                            height--;

                        }
                    } else {


                        waterHeight = airHeight;
                        if(rdata.depthColumn.depths != null) {              
                            if(data.Colormap.WaterHeightmapCompatible && Global.Settings.PREFERHEIGHTMAPS) {
                                short wh = HeightmapFilter.FilterWater(data, cx, cz, waterHeight);
                                if(wh < waterHeight) waterHeight = wh;
                            }
                            while((data.GetBlock(cx, cz, waterHeight) == data.Colormap.depth || data.Colormap.FilterManager.GetBlockVal(data.GetBlock(cx, cz, waterHeight)).ABSORBTION == 0) && waterHeight > 0) waterHeight--;
                        }

                        if(airHeight != waterHeight) {
                            rdata.depthColumn.heights[regionIndex] = airHeight;
                            rdata.depthColumn.depths[regionIndex] = (short)(airHeight - waterHeight);

                            ushort terrid = data.GetBlock(cx, cz, waterHeight);
                            uint terrcolor = data.Colormap.BaseColor(terrid);
                            Tint terrtint = data.Colormap.TintManager.GetBlockVal(terrid);
                            Tint wattint = data.Colormap.TintManager.GetBlockVal(data.GetBlock(cx, cz, airHeight));

                            rdata.depthColumn.biomeIds10_groupIds6[regionIndex] = RawDataColumn.BiomeGroupMaker(data.GetBiome(cx, cz, airHeight), (byte)data.Colormap.Grouping.GetId(data.Colormap.FilterManager.Depth, wattint, !data.Colormap.noShades.Contains(terrid)));
                            rdata.depthColumn.color24_light4_none4[regionIndex] = RawDataColumn.ColorLightMaker(terrtint.GetTintedColor(terrcolor, data.GetBiome(cx, cz, waterHeight), waterHeight), Math.Max(data.GetBlockLight(cx, cz, airHeight), data.GetBlockLight(cx, cz, airHeight + 1)));
                        } else {
                            rdata.depthColumn.heights[regionIndex] = airHeight;

                            ushort id = data.GetBlock(cx, cz, airHeight);
                            Tint tint = data.Colormap.TintManager.GetBlockVal(id);
                            Filter filter = data.Colormap.FilterManager.GetBlockVal(id);

                            rdata.depthColumn.biomeIds10_groupIds6[regionIndex] = RawDataColumn.BiomeGroupMaker(data.GetBiome(cx, cz, airHeight), (byte)data.Colormap.Grouping.GetId(filter, tint, !data.Colormap.noShades.Contains(id)));
                            rdata.depthColumn.color24_light4_none4[regionIndex] = RawDataColumn.ColorLightMaker(data.Colormap.BaseColor(id), Math.Max(data.GetBlockLight(cx, cz, airHeight), data.GetBlockLight(cx, cz, airHeight + 1)));
                        }
                    }





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



                        for(int h = Math.Min(waterHeight, airHeight); h >= 0; h--) {
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
