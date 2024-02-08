using SharpNBT;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Diagnostics;

namespace Mcasaenk.Rendering
{
    public class ChunkRenderer {
        public static void DrawChunk(ChunkRenderData117 data, int x, int z, int[] pixelBuffer, int[] waterPixels, short[] terrainHeights, short[] waterHeights, int height) {
            if(data == null) return;
            if(data.ContainsInformation() == false) return;

            int absHeight = height + 64;

            for(int cx = 0; cx < 16; cx++) {
                for(int cz = 0; cz < 16; cz++) {

                    //loop over sections
                    bool waterDepth = false;
                    for(int i = 24 - (24 - (absHeight >> 4)); i >= 0; i--) {
                        if(data.blockState(i) == null || data.palette(i) == null) {
                            continue;
                        }

                        long[] blockStates = data.blockState(i);
                        var palette = data.palette(i);

                        int sectionHeight = (i - 4) * 16;

                        int bits = (int)data.blockStateSize(i) >> 6;
                        int clean = (int)Math.Pow(2, bits) - 1;

                        int startHeight;
                        if(absHeight >> 4 == i) {
                            startHeight = 16 - (16 - absHeight % 16);
                        } else {
                            startHeight = 16 - 1;
                        }

                        for(int cy = startHeight; cy >= 0; cy--) {
                            int paletteIndex = getPaletteIndex(getIndex(cx, cy, cz, 16), blockStates, bits, clean);
                            if(paletteIndex >= palette.Count) continue;
                            var blockName = palette[paletteIndex];

                            if(isEmpty(blockName)) {
                                continue;
                            }

                            int biome = getBiomeAtBlock(data.biomes, data.biomeSize, cx, sectionHeight + cy, cz);
                            biome = Math.Clamp(biome, 0, 255);
                            //if(!applyBiomeTint) biome = ColorMapping.DEFAULT_BIOME;

                            int regionIndex = (z + cz) * 512 + x + cx;

                            if(!waterDepth) {
                                pixelBuffer[regionIndex] = ColorMapping.GetColor(blockName, biome); // water color
                                waterHeights[regionIndex] = (short)(sectionHeight + cy); // height of highest water or terrain block
                            }
                            if(isWater(blockName)) {
                                waterDepth = true;
                                continue;
                            } else if(isWaterlogged(null)) { // TODO
                                pixelBuffer[regionIndex] = ColorMapping.GetColor(waterDummy, biome); // water color
                                waterPixels[regionIndex] = ColorMapping.GetColor(blockName, biome); // color of waterlogged block
                                waterHeights[regionIndex] = (short)(sectionHeight + cy);
                                terrainHeights[regionIndex] = (short)(sectionHeight + cy - 1); // "height" of bottom of water, which will just be 1 block lower so shading works
                                goto zLoop;
                            } else {
                                waterPixels[regionIndex] = ColorMapping.GetColor(blockName, biome); // color of block at bottom of water
                            }

                            terrainHeights[regionIndex] = (short)(sectionHeight + cy); // height of bottom of water
                            goto zLoop;
                        }
                    }

                zLoop: { }
                }
            }
        }

        private static string waterDummy = "minecraft:water";
        private static int getIndex(int x, int y, int z, int stride) {
            return y * stride * stride + z * stride + x;
        }

        private static int getPaletteIndex(int index, long[] blockStates, int bits, int clean) {
            int indicesPerLong = (int)(64D / bits);
            int blockStatesIndex = index / indicesPerLong;
            int startBit = index % indicesPerLong * bits;
            return (int)(blockStates[blockStatesIndex] >> startBit) & clean;
        }

        private static bool isEmpty(string blockname) {
            return blockname switch {
                "minecraft:air" or "minecraft:cave_air" or "minecraft:barrier" => true, //!!
                _ => false,
            };
        }

        private static bool isWater(string blockname) {
            return blockname switch {
                "minecraft:water" or "minecraft:bubble_column" or "minecraft:barrier" => true, //!!
                _ => false,
            };
        }
        private static bool isWaterlogged(CompoundTag data) {
            //var properties = data.GetValue<CompoundTag>("Properties");
            //var waterlogged = properties?.GetValue<StringTag>("waterlogged");
            //return waterlogged?.Value == "true";
            return false;
        }



        private static int getBiomeAtBlock(int[] biomes, int len, int biomeX, int biomeY, int biomeZ) {
            if(biomes == null) {
                return -1;
            }
            if(len == 1536) {
                biomeY += 64; // adjust for negative y block coordinates
            } else if(len != 1024) { // still support 256 height
                return -1;
            }
            return biomes[getIndex(biomeX / 4, biomeY / 4, biomeZ / 4, 4)];
        }
    }

}
