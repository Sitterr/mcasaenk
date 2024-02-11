using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Xml.Linq;

namespace Mcasaenk.Rendering
{
    public class ChunkRenderer {
        public static void DrawChunk(ChunkRenderData117 data, IColorMapping colorMapping, int x, int z, int[] pixelBuffer, int[] waterPixels, short[] terrainHeights, short[] waterHeights, int plusHeight, int minusHeight) {
            if(data == null) return;
            if(data.ContainsInformation() == false) return;

            for(int cx = 0; cx < 16; cx++) {
                for(int cz = 0; cz < 16; cz++) {

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
                            int regionIndex = (z + cz) * 512 + x + cx;
                            var block = data.GetBlock(cx, cz, cy, i);

                            if(isEmpty(block)) {
                                continue;
                            }
                            if(isWater(block) && waterDepth) {
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

                                if(isWater(block)) {
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

        private static bool isEmpty(string blockname) {
            return blockname switch {
                "minecraft:air" or "minecraft:cave_air" or "minecraft:barrier" => true,
                _ => false,
            };
        }
        private static bool isWater(string blockname) {
            return blockname switch {
                "minecraft:water" or "minecraft:bubble_column" or "minecraft:barrier" => true,
                _ => false,
            };
        }

    }

}
