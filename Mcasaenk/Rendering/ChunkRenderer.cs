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

namespace Mcasaenk.Rendering
{
    public class ChunkRenderer {
        public static void Extract(IChunkInterpreter data, IColorMapping colorMapping, int x, int z, GenerateTilePool.RawData rdata, int plusHeight, int minusHeight) {
            if(data == null) return;
            if(data.ContainsInformation() == false) return;

            for(int cx = 0; cx < 16; cx++) {
                for(int cz = 0; cz < 16; cz++) {

                    int regionIndex = (z + cz) * 512 + x + cx;

                    //loop over sections
                    bool waterDepth = false;
                    for(int i = plusHeight / 16; i >= minusHeight / 16; i--) {
                        if(data.CanSkipSection(i)) {
                            continue;
                        }

                        int sectionHeight = i * 16;

                        int startHeight;
                        if(plusHeight / 16 == i) {
                            startHeight = plusHeight % 16;
                        } else {
                            startHeight = 16 - 1;
                        }

                        for(int cy = startHeight; cy >= 0; cy--) {
                            int h = sectionHeight + cy;
                            var block = data.GetBlock(cx, cz, sectionHeight + cy);

                            bool isEmpty = IsEmpty(block), isWater = IsWater(block);
                            if(isEmpty) {
                                continue;
                            }
                            if(isWater && waterDepth) {
                                continue;
                            }

                            if(!waterDepth) {
                                rdata.biomeIds[regionIndex] = data.GetBiome(cx, cz, h); // water biome
                                rdata.heights[regionIndex] = (short)(h); // height of highest water or terrain block

                                if(isWater && Settings.WATER) {
                                    waterDepth = true;
                                    continue;
                                }
                            }

                            rdata.blockIds[regionIndex] = block;
                            rdata.terrainHeights[regionIndex] = (short)(h); // height of bottom of water
                            //rdata.terrainHeights[regionIndex] = data.GetTerrainHeight(cx, cz);
                            goto zLoop;
                        }
                    }

                zLoop: { }
                }
            }
        }
        public static void ExtractWithHeightmaps(IChunkInterpreter data, IColorMapping colorMapping, int x, int z, GenerateTilePool.RawData rdata, int plusHeight, int minusHeight) {
            if(data == null) return;
            if(data.ContainsInformation() == false) return;


            for(int cx = 0; cx < 16; cx++) {
                for(int cz = 0; cz < 16; cz++) {
                    int regionIndex = (z + cz) * 512 + x + cx;

                    short height = (short)(data.GetHeight(cx, cz) - 1);
                    rdata.heights[regionIndex] = height;
                    rdata.biomeIds[regionIndex] = data.GetBiome(cx, cz, height);
                    ushort hBlock = data.GetBlock(cx, cz, height, true);

                    if(Settings.WATER) {
                        short theight = (short)(data.GetTerrainHeight(cx, cz) - 1);
                        rdata.terrainHeights[regionIndex] = theight;
                        ushort tBlock = data.GetBlock(cx, cz, theight, true);
                        rdata.blockIds[regionIndex] = tBlock;

                        if(height != theight && hBlock != ColorMapping.BLOCK_WATER) {
                            rdata.terrainHeights[regionIndex] = height;
                        }
                    } else {
                        rdata.blockIds[regionIndex] = hBlock;
                    }
                }
            }
        }

        public static void Extract3D(IChunkInterpreter data, IColorMapping colorMapping, int x, int z, GenerateTilePool.RawData rdata, int plusHeight, int minusHeight) {
            if(data == null) return;
            if(data.ContainsInformation() == false) return;

            int x0 = ShadeConstants.GLB.nflowX(0, 0, ShadeConstants.GLB.rX) * 512;
            int z0 = ShadeConstants.GLB.nflowZ(0, 0, ShadeConstants.GLB.rZ) * 512;
            int SHADEX = ShadeConstants.GLB.rX * 512, SHADEZ = ShadeConstants.GLB.rZ * 512;

            for(int _cz = 0; _cz < 16; _cz++) {
                int cz = ShadeConstants.GLB.flowZ(_cz, 0, 16);
                for(int _cx = 0; _cx < 16; _cx++) {
                    int cx = ShadeConstants.GLB.flowX(_cx, 0, 16);

                    int regionIndex = (z + cz) * 512 + x + cx;

                    //loop over sections
                    bool waterDepth = false, done = false;
                    for(int i = plusHeight / 16; i >= minusHeight / 16; i--) {
                        if(data.CanSkipSection(i)) {
                            continue;
                        }

                        int sectionHeight = i * 16;

                        int startHeight;
                        if(plusHeight / 16 == i) {
                            startHeight = plusHeight % 16;
                        } else {
                            startHeight = 16 - 1;
                        }

                        for(int cy = startHeight; cy >= 0; cy--) {
                            int h = sectionHeight + cy;
                            var block = data.GetBlock(cx, cz, sectionHeight + cy);

                            bool isEmpty = IsEmpty(block), isWater = IsWater(block);

                            if(isEmpty) {
                                continue;
                            }
                            if(isWater && waterDepth) {
                                continue;
                            }

                            uint blockColor = colorMapping.GetColor(block, data.GetBiome(cx, cz, h));
                            if(blockColor >> 24 == 0) { // another IsEmpty check
                                continue;
                            }



                            int hs = 319 - (sectionHeight + minusHeight + cy);
                            double x1 = (x0 + x + cx) + ShadeConstants.GLB.cosAcotgB * hs, z1 = (z0 + z + cz) + -ShadeConstants.GLB.sinAcotgB * hs;

                            bool alreadyshade = CheckLine(rdata.shadeFrame, SHADEX, SHADEZ, x1, z1);

                            if(!waterDepth) {
                                if(!done) {
                                    rdata.biomeIds[regionIndex] = data.GetBiome(cx, cz, h); // water biome
                                    rdata.heights[regionIndex] = (short)(h); // height of highest water or terrain block
                                    SetShadeValuesLine(rdata.shadeFrame, rdata.shadeValues, ref rdata.shadeValuesLen.Get()[regionIndex], regionIndex, SHADEX, SHADEZ, x1, z1);
                                }

                                if(isWater && Settings.WATER) {
                                    waterDepth = true;
                                    continue;
                                }
                            }


                            if(!alreadyshade) {
                                SetLine(rdata.shadeFrame, true, SHADEX, SHADEZ, x1, z1);
                            }

                            if(!done) {
                                rdata.blockIds[regionIndex] = block;
                                rdata.terrainHeights[regionIndex] = (short)(sectionHeight + cy); // height of bottom of water
                                //goto zLoop;
                                done = true;
                            }
                        }
                    }

                zLoop: { }
                }
            }
        }




        static void SetLine(bool[] shadeFrame, bool value, int SHADEX, int SHADEZ, double _x1, double _z1) {
            int x1 = (int)_x1, z1 = (int)_z1;
            double w = Math.Abs(ShadeConstants.GLB.cosAcotgB), h = Math.Abs(ShadeConstants.GLB.sinAcotgB);
            w = w % 1; h = h % 1;
            _x1 = _x1 % 1; _z1 = _z1 % 1;

            List<Point2i> blockReach;
            if(_x1 + w > 1 && _z1 + h > 1) blockReach = ShadeConstants.GLB.blockReachCC;
            else if(_x1 + w > 1) blockReach = ShadeConstants.GLB.blockReachCF;
            else if(_z1 + h > 1) blockReach = ShadeConstants.GLB.blockReachFC;
            else blockReach = ShadeConstants.GLB.blockReachFF;

            foreach(var p in blockReach) {
                if((z1 + p.Z) < 0 || (z1 + p.Z) >= SHADEZ) continue;
                if((x1 + p.X) < 0 || (x1 + p.X) >= SHADEX) continue;
                shadeFrame[(z1 + p.Z) * SHADEX + (x1 + p.X)] = value;
            }
        }
        static bool CheckLine(bool[] shadeFrame, int SHADEX, int SHADEZ, double _x1, double _z1) {
            int x1 = (int)_x1, z1 = (int)_z1;
            double w = Math.Abs(ShadeConstants.GLB.cosAcotgB), h = Math.Abs(ShadeConstants.GLB.sinAcotgB);
            w = w % 1; h = h % 1;
            _x1 = _x1 % 1; _z1 = _z1 % 1;

            List<Point2i> blockReach;
            if(_x1 + w > 1 && _z1 + h > 1) blockReach = ShadeConstants.GLB.blockReachCC;
            else if(_x1 + w > 1) blockReach = ShadeConstants.GLB.blockReachCF;
            else if(_z1 + h > 1) blockReach = ShadeConstants.GLB.blockReachFC;
            else blockReach = ShadeConstants.GLB.blockReachFF;

            foreach(var p in blockReach) {
                if((z1 + p.Z) < 0 || (z1 + p.Z) >= SHADEZ) continue;
                if((x1 + p.X) < 0 || (x1 + p.X) >= SHADEX) continue;
                if(shadeFrame[(z1 + p.Z) * SHADEX + (x1 + p.X)] == false) return false;
            }
            return true;
        }

        public static void SetShadeValuesLine(bool[] shadeFrame, bool[] shades, ref byte shadesLen, int regionIndex, int SHADEX, int SHADEZ, double _x1, double _z1) {
            int x1 = (int)_x1, z1 = (int)_z1;
            double w = Math.Abs(ShadeConstants.GLB.cosAcotgB), h = Math.Abs(ShadeConstants.GLB.sinAcotgB);
            w = w % 1; h = h % 1;
            _x1 = _x1 % 1; _z1 = _z1 % 1;

            List<Point2i> blockReach;
            if(_x1 + w > 1 && _z1 + h > 1) blockReach = ShadeConstants.GLB.blockReachCC;
            else if(_x1 + w > 1) blockReach = ShadeConstants.GLB.blockReachCF;
            else if(_z1 + h > 1) blockReach = ShadeConstants.GLB.blockReachFC;
            else blockReach = ShadeConstants.GLB.blockReachFF;

            for(int i = 0; i < blockReach.Count; i++) {
                var p = blockReach[i];
                shadesLen = (byte)blockReach.Count;

                if(((z1 + p.Z) < 0 || (z1 + p.Z) >= SHADEZ) || ((x1 + p.X) < 0 || (x1 + p.X) >= SHADEX)) {
                    shades[regionIndex * ShadeConstants.GLB.blockReachLenMax + i] = true;
                    continue;
                }

                bool val = shadeFrame[(z1 + p.Z) * SHADEX + (x1 + p.X)];
                shades[regionIndex * ShadeConstants.GLB.blockReachLenMax + i] |= val;
            }
        }



        static bool IsEmpty(ushort blockid) {
            if(blockid == ColorMapping.BLOCK_AIR) return true;
            return false;
        }
        static bool IsWater(ushort blockid) {
            if(blockid == ColorMapping.BLOCK_WATER) return true;
            return false;
        }
    }

}
