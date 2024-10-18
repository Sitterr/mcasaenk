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




                    short airHeight = Filter.AIR_FILTER(data, y, maxh - 1)(data, cx, cz, (short)y);
                    if(rdata.columns.Length > 0) {
                        short height = airHeight;

                        ushort blockid = data.GetBlock(cx, cz, height);
                        BlockValue block = data.Colormap.Value(blockid);
                        int coli = 0;
                        Group lastgroup = block.group;
                        uint color = block.color;
                        float lcolor = lastgroup.ABSORBTION / 15f;
                            
                        if(height < 0) continue;

                        short startheight = height; height--;
                        byte startlight = Math.Max(data.GetBlockLight(cx, cz, startheight), data.GetBlockLight(cx, cz, startheight + 1));
                        ushort startbiome = data.GetBiome(cx, cz, startheight);

                        while(true) {
                            blockid = data.GetBlock(cx, cz, height);
                            block = data.Colormap.Value(blockid);
                            ushort biome = data.GetBiome(cx, cz, height);

                            if(block.group != lastgroup || biome != startbiome || (lastgroup.ABSORBTION == 15 && !lastgroup.hostdepth)) {

                                if(lastgroup.ABSORBTION > 0) {

                                    if(lastgroup.hostdepth) {
                                        rdata.depthColumn.heights[regionIndex] = startheight;
                                        rdata.depthColumn.depths[regionIndex] = (short)(startheight - height);

                                        rdata.depthColumn.biomeIds10_groupIds6[regionIndex] = RawDataColumn.BiomeGroupMaker(startbiome, (byte)2);
                                        rdata.depthColumn.color24_light4_none4[regionIndex] = RawDataColumn.ColorLightMaker(block.GetColor(biome, height), startlight);

                                        break;
                                    } else {
                                        RawDataColumn col;
                                        if(coli == rdata.columns.Length) col = rdata.depthColumn;
                                        else col = rdata.columns[coli];


                                        col.biomeIds10_groupIds6[regionIndex] = RawDataColumn.BiomeGroupMaker(startbiome, (byte)lastgroup.GetId());
                                        col.heights[regionIndex] = startheight;
                                        if(col != rdata.depthColumn) col.depths[regionIndex] = (short)(startheight - height);
                                        col.color24_light4_none4[regionIndex] = RawDataColumn.ColorLightMaker(color, startlight);
                                        coli++;
                                    }

                                }
                                if(lastgroup.ABSORBTION == 15) break;

                                startheight = height;
                                lastgroup = block.group;
                                startbiome = biome;
                                lcolor = lastgroup.ABSORBTION / 15f;
                                color = block.color;
                                if(coli >= rdata.columns.Length + 1) break;
                            } else {
                                float q = lastgroup.ABSORBTION / 15f * (1 - lcolor);
                                color = Global.Blend(block.color, color, q / lcolor);
                                lcolor += q;
                            }

                            height--;

                        }
                    } else {
                        short waterHeight = Filter.DEPTH_FILTER(data, y, maxh - 1)(data, cx, cz, airHeight);
                        if(airHeight != waterHeight && rdata.depthColumn.depths != null) {
                            rdata.depthColumn.heights[regionIndex] = airHeight;
                            rdata.depthColumn.depths[regionIndex] = (short)(airHeight - waterHeight);

                            var block = data.Colormap.Value(data.GetBlock(cx, cz, waterHeight));
                            rdata.depthColumn.biomeIds10_groupIds6[regionIndex] = RawDataColumn.BiomeGroupMaker(data.GetBiome(cx, cz, airHeight), 2);
                            rdata.depthColumn.color24_light4_none4[regionIndex] = RawDataColumn.ColorLightMaker(block.GetColor(data.GetBiome(cx, cz, waterHeight), waterHeight), Math.Max(data.GetBlockLight(cx, cz, airHeight), data.GetBlockLight(cx, cz, airHeight + 1)));
                        } else {
                            rdata.depthColumn.heights[regionIndex] = airHeight;

                            var block = data.Colormap.Value(data.GetBlock(cx, cz, airHeight));
                            rdata.depthColumn.biomeIds10_groupIds6[regionIndex] = RawDataColumn.BiomeGroupMaker(data.GetBiome(cx, cz, airHeight), (byte)block.group.GetId());
                            rdata.depthColumn.color24_light4_none4[regionIndex] = RawDataColumn.ColorLightMaker(block.color, Math.Max(data.GetBlockLight(cx, cz, airHeight), data.GetBlockLight(cx, cz, airHeight + 1)));
                        }
                    }





                    if(rdata.shadeFrame != null) {
                        foreach(var col in rdata.columns) {
                            if(col.ContainsInfo(regionIndex) == false) continue;

                            int hs = maxh - col.heights[regionIndex];
                            double x1 = xtotal + ShadeConstants.GLB.cosAcotgB * hs, z1 = ztotal + -ShadeConstants.GLB.sinAcotgB * hs;
                            SetShadeValuesLine(rdata.shadeFrame, col.shadeValues, regionIndex, SHADEX, SHADEZ, x1, z1);
                        }
                        if(rdata.depthColumn.ContainsInfo(regionIndex)) {
                            int hs = maxh - (rdata.depthColumn.heights[regionIndex] + (rdata.depthColumn.depths != null ? rdata.depthColumn.depths[regionIndex] : 0));
                            double x1 = xtotal + ShadeConstants.GLB.cosAcotgB * hs, z1 = ztotal + -ShadeConstants.GLB.sinAcotgB * hs;
                            SetShadeValuesLine(rdata.shadeFrame, rdata.depthColumn.shadeValues, regionIndex, SHADEX, SHADEZ, x1, z1);
                        }



                        for(int height = airHeight; height >= 0; height--) {
                            //short nheight = Shade3DFilter.List(data, cx, cz, height);
                            var blid = data.GetBlock(cx, cz, height);
                            if(Shade3DFilter.IsShade3D(blid)) continue;
                            var block = data.Colormap.Value(blid);

                            //if(height <= 0) break;

                            int hs = maxh - height;
                            double x1 = xtotal + ShadeConstants.GLB.cosAcotgB * hs, z1 = ztotal + -ShadeConstants.GLB.sinAcotgB * hs;
                            //bool alreadyshade = CheckLine(rdata.shadeFrame, SHADEX, SHADEZ, x1, z1);
                            //if(!alreadyshade) {
                            SetLine(rdata.shadeFrame, (byte)block.group.ABSORBTION, SHADEX, SHADEZ, x1, z1);
                            //}
                        }


                    }
                }
            }

        }


        static void SetLine(byte[] shadeFrame, byte value, int SHADEX, int SHADEZ, double _x1, double _z1) {
            int x1 = (int)(_x1), z1 = (int)(_z1);

            foreach(var reach in ShadeConstants.GLB.blockReach) {
                var p = reach.p;
                int i = (z1 + p.Z) * SHADEX + (x1 + p.X);

                //if((z1 + p.p.Z) < 0 || (z1 + p.p.Z) >= SHADEZ) 
                //    continue;
                //if((x1 + p.p.X) < 0 || (x1 + p.p.X) >= SHADEX) 
                //    continue;

                if(reach.dir == ShadeConstants.RegionDir.l || reach.dir == ShadeConstants.RegionDir.c)
                    ShadeConstants.SetLeft(shadeFrame, i, ShadeConstants.CombineShades(ShadeConstants.GetLeft(shadeFrame, i), value));

                if(reach.dir == ShadeConstants.RegionDir.r || reach.dir == ShadeConstants.RegionDir.c)
                    ShadeConstants.SetRight(shadeFrame, i, ShadeConstants.CombineShades(ShadeConstants.GetRight(shadeFrame, i), value));
            }
        }

        public static void SetShadeValuesLine(byte[] shadeFrame, byte[] shades, int regionIndex, int SHADEX, int SHADEZ, double _x1, double _z1) {
            int x1 = (int)_x1, z1 = (int)_z1;

            var blockReach = ShadeConstants.GLB.blockReach;

            for(int i = 0; i < blockReach.Count; i++) {
                var p = blockReach[i].p;

                if(((z1 + p.Z) < 0 || (z1 + p.Z) >= SHADEZ) || ((x1 + p.X) < 0 || (x1 + p.X) >= SHADEX)) {
                    //ShadeConstants.SetS(shades, regionIndex * ShadeConstants.GLB.blockReachLenMax + i, 0);
                    continue;
                }

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

    }

}
