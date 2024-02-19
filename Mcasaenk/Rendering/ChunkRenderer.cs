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
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Windows.Shapes;
using Accessibility;
using System.Windows.Controls;

namespace Mcasaenk.Rendering
{
    public class ChunkRenderer {
        public static void DrawChunk(ChunkRenderData117 data, IColorMapping colorMapping, int x, int z, int[] pixelBuffer, int[] waterPixels, short[] terrainHeights, short[] waterHeights, int plusHeight, int minusHeight) {
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
                            var block = data.GetBlock(cx, cz, cy, i);

                            if(IsEmpty(block)) {
                                continue;
                            }
                            if(IsWater(block) && waterDepth) {
                                continue;
                            }

                            BlockInformation blockInformation = new BlockInformation() {
                                biome = data.GetBiome(cx, cz, cy, i),
                                height = sectionHeight + cy,
                            };
                            uint blockColor = colorMapping.GetColor(block, blockInformation);

                            if(blockColor >> 24 == 0) { // another IsEmprty check
                                continue;
                            }

                            if(!waterDepth) {
                                pixelBuffer[regionIndex] = (int)blockColor; // water color
                                waterHeights[regionIndex] = (short)(sectionHeight + cy); // height of highest water or terrain block

                                if(IsWater(block)) {
                                    waterDepth = true;
                                    continue;
                                }
                            }

                            waterPixels[regionIndex] = (int)blockColor; // color of block at bottom of water
                            terrainHeights[regionIndex] = (short)(sectionHeight + cy); // height of bottom of water
                            goto zLoop;
                        }
                    }

                zLoop: { }
                }
            }
        }

        public static void DrawChunk3D(ChunkRenderData117 data, IColorMapping colorMapping, int x, int z, int[] pixelBuffer, int[] waterPixels, short[] terrainHeights, short[] waterHeights, bool[] shadeFrame, bool[] shadeValues, byte[] shadeValuesLen, int plusHeight, int minusHeight) {
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
                            var block = data.GetBlock(cx, cz, cy, i);

                            bool isEmpty = IsEmpty(block), isWater = IsWater(block);

                            if(isEmpty) {
                                continue;
                            }
                            if(isWater && waterDepth) {
                                continue;
                            }

                            BlockInformation blockInformation = new BlockInformation() {
                                biome = data.GetBiome(cx, cz, cy, i),
                                height = sectionHeight + cy,
                            };
                            uint blockColor = colorMapping.GetColor(block, blockInformation);

                            if(blockColor >> 24 == 0) { // another IsEmpty check
                                continue;
                            }



                            int h = 319 - (sectionHeight + minusHeight + cy);
                            double x1 = (x0 + x + cx) + ShadeConstants.GLB.cosAcotgB * h, z1 = (z0 + z + cz) + -ShadeConstants.GLB.sinAcotgB * h;

                            bool alreadyshade = CheckLine(shadeFrame, SHADEX, SHADEZ, x1, z1);

                            uint unshadedBlockColor = blockColor;
                            if(alreadyshade) blockColor = Global.AddShade(blockColor, Settings.SHADE3DMOODYNESS, Settings.SHADE3DMOODYNESS, Settings.SHADE3DMOODYNESS);

                            if(!waterDepth) {
                                if(!done) {
                                    pixelBuffer[regionIndex] = (int)blockColor; // water color
                                    waterHeights[regionIndex] = (short)(sectionHeight + cy); // height of highest water or terrain block
                                    SetShadeValuesLine(shadeFrame, shadeValues, ref shadeValuesLen[regionIndex], regionIndex, SHADEX, SHADEZ, x1, z1);
                                }

                                if(isWater) {
                                    waterDepth = true;
                                    continue;
                                }
                            }


                            if(!alreadyshade) {
                                SetLine(shadeFrame, true, SHADEX, SHADEZ, x1, z1);
                            }

                            if(!done) {
                                waterPixels[regionIndex] = (int)unshadedBlockColor; // color of block at bottom of water
                                terrainHeights[regionIndex] = (short)(sectionHeight + cy); // height of bottom of water
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






        static bool IsEmpty(string blockname) {
            return blockname switch {
                "minecraft:air" or "minecraft:cave_air" or "minecraft:barrier" => true,
                _ => false,
            };
        }
        static bool IsWater(string blockname) {
            return blockname switch {
                "minecraft:water" or "minecraft:bubble_column" or "minecraft:barrier" => true,
                _ => false,
            };
        }
    }

}
