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

namespace Mcasaenk.Rendering
{
    public class ChunkRenderer {
        public static void Extract(IChunkInterpreter data, int x, int z, int y, RawData rdata) {
            try {
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

                        short airHeight = Filter.AIR_FILTER(y, maxh - 1)(data, cx, cz, (short)y);
                        rdata.heights[regionIndex] = airHeight;
                        rdata.biomeIds[regionIndex] = data.GetBiome(cx, cz, airHeight);

                        short waterHeight = Filter.DEPTH_FILTER(y, maxh - 1)(data, cx, cz, airHeight);
                        rdata.blockIds[regionIndex] = data.GetBlock(cx, cz, waterHeight);
                        rdata.terrainHeights[regionIndex] = waterHeight;

                        rdata.blockLights[regionIndex] = Math.Max(data.GetBlockLight(cx, cz, airHeight), data.GetBlockLight(cx, cz, airHeight + 1));

                        if(rdata.shadeFrame != null && rdata.shadeValues != null && rdata.shadeValuesLen != null) {
                            {
                                int hs = maxh - airHeight;
                                double x1 = xtotal + ShadeConstants.GLB.cosAcotgB * hs, z1 = ztotal + -ShadeConstants.GLB.sinAcotgB * hs;
                                SetShadeValuesLine(rdata.shadeFrame, rdata.shadeValues, ref rdata.shadeValuesLen[regionIndex], regionIndex, SHADEX, SHADEZ, x1, z1);
                            }

                            for(int height = waterHeight; height >= 0; height--) {
                                //short nheight = Shade3DFilter.List(data, cx, cz, height);
                                var bl = data.GetBlock(cx, cz, height);
                                if(Shade3DFilter.IsShade3D(bl)) continue;

                                //if(height <= 0) break;

                                int hs = maxh - height;
                                double x1 = xtotal + ShadeConstants.GLB.cosAcotgB * hs, z1 = ztotal + -ShadeConstants.GLB.sinAcotgB * hs;
                                //bool alreadyshade = CheckLine(rdata.shadeFrame, SHADEX, SHADEZ, x1, z1);
                                //if(!alreadyshade) {
                                SetLine(rdata.shadeFrame, true, SHADEX, SHADEZ, x1, z1);
                                //}

                            }

                        }
                    }
                }
            }
            catch(Exception e) {
                for(int _cx = 0; _cx < 16; _cx++) {
                    int cx = ShadeConstants.GLB.flowX(_cx, 0, 16);
                    for(int _cz = 0; _cz < 16; _cz++) {
                        int cz = ShadeConstants.GLB.flowZ(_cz, 0, 16);
                        int regionIndex = (z + cz) * 512 + x + cx;

                        rdata.blockIds[regionIndex] = Colormap.ERRORBLOCK;
                    }
                }
#if DEBUG
                throw e;
#endif
            }
        }


        static void SetLine(bool[] shadeFrame, bool value, int SHADEX, int SHADEZ, double _x1, double _z1) {
            int x1 = (int)_x1, z1 = (int)_z1;

            foreach(var p in ShadeConstants.GLB.blockReach) {
                //if((z1 + p.p.Z) < 0 || (z1 + p.p.Z) >= SHADEZ) 
                //    continue;
                //if((x1 + p.p.X) < 0 || (x1 + p.p.X) >= SHADEX) 
                //    continue;
                shadeFrame[(z1 + p.Z) * SHADEX + (x1 + p.X)] = value;
            }
        }
        static bool CheckLine(bool[] shadeFrame, int SHADEX, int SHADEZ, double _x1, double _z1) {
            int x1 = (int)_x1, z1 = (int)_z1;

            foreach(var p in ShadeConstants.GLB.blockReach) {
                if((z1 + p.Z) < 0 || (z1 + p.Z) >= SHADEZ) 
                    continue;
                if((x1 + p.X) < 0 || (x1 + p.X) >= SHADEX)
                    continue;
                if(shadeFrame[(z1 + p.Z) * SHADEX + (x1 + p.X)] == false) return false;
            }
            return true;
        }
        public static void SetShadeValuesLine(bool[] shadeFrame, bool[] shades, ref byte shadesLen, int regionIndex, int SHADEX, int SHADEZ, double _x1, double _z1) {
            int x1 = (int)_x1, z1 = (int)_z1;

            var blockReach = ShadeConstants.GLB.blockReach;

            shadesLen = (byte)blockReach.Length;
            for(int i = 0; i < blockReach.Length; i++) {
                var p = blockReach[i];

                if(((z1 + p.Z) < 0 || (z1 + p.Z) >= SHADEZ) || ((x1 + p.X) < 0 || (x1 + p.X) >= SHADEX)) {
                    shades[regionIndex * ShadeConstants.GLB.blockReachLenMax + i] = false;
                    continue;
                }

                bool val = shadeFrame[(z1 + p.Z) * SHADEX + (x1 + p.X)];
                shades[regionIndex * ShadeConstants.GLB.blockReachLenMax + i] |= val;
            }
        }

    }

}
