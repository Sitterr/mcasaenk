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
using static System.Net.WebRequestMethods;

namespace Mcasaenk.Rendering
{
    class TileImage {
        private Tile tile;
        private WriteableBitmap img;
        public TileImage(Tile tile) {
            this.tile = tile;
        }
        public ImageSource GetImage() {
            return img;
        }


        public void GenerateForreal() {
            /*
            za vseki region:
                1) heightmap, bool[20], existing pixels
                2) List<(bool[512*512] data, Point origin)> tileshadedatas;
             */
            int[] pixelBuffer = PoolHandler.pixelBuffer.Rent(512 * 512);

            
            HardGenerate(pixelBuffer);
            img = GenerateBitmap(pixelBuffer);
            

            PoolHandler.pixelBuffer.Return(pixelBuffer, true);
        }

        private void HardGenerate(int[] pixelBuffer) {
            int[] waterPixels = PoolHandler.waterPixels.Rent(512 * 512);
            short[] terrainHeights = PoolHandler.terrainHeights.Rent(512 * 512);
            short[] waterHeights = PoolHandler.waterHeights.Rent(512 * 512);

            var chunks = RegionReader.ReadAnvilFileWithUnmanaged(tile.GetOrigin().dimension.GetRegionPath(tile.pos));           

            for(int zz = 0; zz < 32; zz++) {
                for(int xx = 0; xx < 32; xx++) {
                    ChunkRenderer.DrawChunk(chunks[zz * 32 + xx], ColorMapping.Current, xx * 16, zz * 16, pixelBuffer, waterPixels, terrainHeights, waterHeights, 319, -64);

                    chunks[zz * 32 + xx]?.Dispose();
                }
            }



            shade(pixelBuffer, waterPixels, terrainHeights, waterHeights, Math.Cos(Math.PI / 4), Math.Sin(Math.PI / 4));

            PoolHandler.waterPixels.Return(waterPixels, true);
            PoolHandler.terrainHeights.Return(terrainHeights, true);
            PoolHandler.waterHeights.Return(waterHeights, true);
        }
        private static void shade(int[] pixelBuffer, int[] waterPixels, short[] terrainHeights, short[] waterHeights, double cosA, double sinA) {
            cosA = Math.Round(cosA, 2);
            sinA = Math.Round(sinA, 2);

            int index = 0;
            for(int z = 0; z < 512; z++) {
                for(int x = 0; x < 512; x++, index++) {
                    float xShade, zShade;

                    if(pixelBuffer[index] == 0) {
                        continue;
                    }

                    if(terrainHeights[index] != waterHeights[index]) {
                        float ratio = 0.5f - 0.5f / 40f * (float)((waterHeights[index]) - (terrainHeights[index]));
                        pixelBuffer[index] = (int)Blend((uint)pixelBuffer[index], (uint)waterPixels[index], ratio);
                    } else {
                        if(z == 0) {
                            zShade = (waterHeights[index + 512]) - (waterHeights[index]);
                        } else if(z == 512 - 1) {
                            zShade = (waterHeights[index]) - (waterHeights[index - 512]);
                        } else {
                            zShade = ((waterHeights[index + 512]) - (waterHeights[index - 512])) * 2;
                        }

                        if(x == 0) {
                            xShade = (waterHeights[index + 1]) - (waterHeights[index]);
                        } else if(x == 512 - 1) {
                            xShade = (waterHeights[index]) - (waterHeights[index - 1]);
                        } else {
                            xShade = ((waterHeights[index + 1]) - (waterHeights[index - 1])) * 2;
                        }

                        double shade = -(cosA * xShade + -sinA * zShade);
                        if(shade < -8) {
                            shade = -8;
                        }
                        if(shade > 8) {
                            shade = 8;
                        }

                        int altitudeShade = 16 * (waterHeights[index] - 64) / 255;
                        if(altitudeShade < -4) {
                            altitudeShade = -4;
                        }
                        if(altitudeShade > 24) {
                            altitudeShade = 24;
                        }

                        //shade += altitudeShade;

                        //if(ConfigProvider.WORLD.getRenderingMode() == RenderingMode.SHADE) {
                        //if(shade > 0)
                        //pixelBuffer[index] = Color.shade(pixelBuffer[index], (int)(shade * 3)); 
                        //} else 

                        pixelBuffer[index] = (int)AddShade((uint)pixelBuffer[index], (int)(shade * 8), (int)(shade * 8), (int)(shade * 8));

                    }
                }
            }
        }
        private static uint AddShade(uint color, int ar, int ag, int ab) {
            byte a = (byte)((color >> 24) & 0xFF);
            byte r = (byte)((color >> 16) & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte b = (byte)(color & 0xFF);

            byte r2 = (byte)Math.Clamp(r + ar, 0, 255);
            byte g2 = (byte)Math.Clamp(g + ag, 0, 255);
            byte b2 = (byte)Math.Clamp(b + ab, 0, 255);
            return (uint)((a << 24) | (r2 << 16) | (g2 << 8) | b2);
        }
        private static uint MultShade(uint color, float ar, float ag, float ab) {
            byte a = (byte)((color >> 24) & 0xFF);
            byte r = (byte)((color >> 16) & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte b = (byte)(color & 0xFF);

            byte r2 = (byte)(r * ar);
            byte g2 = (byte)(g * ag);
            byte b2 = (byte)(b * ab);
            return (uint)((a << 24) | (r2 << 16) | (g2 << 8) | b2);
        }
        private static uint Blend(uint color, uint other, float ratio) {
            ratio = Math.Clamp(ratio, 0, 1);
            float oration = 1 - ratio;

            uint aA = color >> 24 & 0xFF;
            uint aR = color >> 16 & 0xFF;
            uint aG = color >> 8 & 0xFF;
            uint aB = color & 0xFF;

            uint bA = other >> 24 & 0xFF;
            uint bR = other >> 16 & 0xFF;
            uint bG = other >> 8 & 0xFF;
            uint bB = other & 0xFF;

            uint a = (uint)(aA * oration + bA * ratio);
            uint r = (uint)(aR * oration + bR * ratio);
            uint g = (uint)(aG * oration + bG * ratio);
            uint b = (uint)(aB * oration + bB * ratio);

            return a << 24 | r << 16 | g << 8 | b;
        }

        private static WriteableBitmap GenerateBitmap(int[] pixels) {
            WriteableBitmap output = new WriteableBitmap(512, 512, 96, 96, PixelFormats.Bgra32, null);

            int stride = (int)output.Width * (output.Format.BitsPerPixel / 8);
            output.WritePixels(new Int32Rect(0, 0, 512, 512), pixels, stride, 0);

            output.Freeze();
            return output;
        }
    }

}
