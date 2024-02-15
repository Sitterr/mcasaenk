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
using Mcasaenk.Shade3d;

namespace Mcasaenk.Rendering
{
    public class TileImage {
        private Tile tile;
        private WriteableBitmap img;
        public TileImage(Tile tile) {
            this.tile = tile;
        }
        public ImageSource GetImage() {
            return img;
        }

        public static long lastRedrawTime = 0;
        public void GenerateForreal() {
            /*
            za vseki region:
                1) heightmap, bool[20], existing pixels
                2) List<(bool[512*512] data, Point origin)> tileshadedatas;
             */
            int[] pixelBuffer = PoolHandler.pixelBuffer.Rent(512 * 512);

            var st = Stopwatch.StartNew();
            HardGenerate(pixelBuffer);
            st.Stop();
            lastRedrawTime = st.ElapsedMilliseconds;

            img = GenerateBitmap(pixelBuffer);
            

            PoolHandler.pixelBuffer.Return(pixelBuffer, true);
        }

        private void HardGenerate(int[] pixelBuffer) {
            int[] waterPixels = PoolHandler.waterPixels.Rent(512 * 512);
            short[] terrainHeights = PoolHandler.terrainHeights.Rent(512 * 512);
            short[] waterHeights = PoolHandler.waterHeights.Rent(512 * 512);
            bool[] shades = null;
            if(Settings.SHADE3D) shades = PoolHandler.shades.Rent((ShadeConstants.GLOBAL.rX * 512) * (ShadeConstants.GLOBAL.rZ * 512));

            var chunks = RegionReader.ReadAnvilFileWithUnmanaged(tile.GetOrigin().dimension.GetRegionPath(tile.pos));           

            for(int _cz = 0; _cz < 32; _cz++) {
                int cz = ShadeConstants.GLOBAL.flowZ(_cz, 0, 32);
                for(int _cx = 0; _cx < 32; _cx++) {
                    int cx = ShadeConstants.GLOBAL.flowX(_cx, 0, 32);
                    var chunk = chunks[cz * 32 + cx];
                    if(chunk == null) continue;

                    if(Settings.SHADE3D) ChunkRenderer.DrawChunk3D(chunk, ColorMapping.Current, cx * 16, cz * 16, pixelBuffer, waterPixels, terrainHeights, waterHeights, shades, 319, -64);
                    else ChunkRenderer.DrawChunk(chunk, ColorMapping.Current, cx * 16, cz * 16, pixelBuffer, waterPixels, terrainHeights, waterHeights, 319, -64);
                    

                    chunk.Dispose();
                }
            }


            { // save shades 
            
            }

            shade(pixelBuffer, waterPixels, terrainHeights, waterHeights, ShadeConstants.GLOBAL.cosA, ShadeConstants.GLOBAL.sinA);        

            PoolHandler.waterPixels.Return(waterPixels, true);
            PoolHandler.terrainHeights.Return(terrainHeights, true);
            PoolHandler.waterHeights.Return(waterHeights, true);
            if(Settings.SHADE3D) PoolHandler.shades.Return(shades, true);
        }

        private unsafe void UpdateShade() { 
            img.Lock();

            byte* pixels = (byte*)img.BackBuffer;

            img.Unlock();
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
                        pixelBuffer[index] = (int)Global.Blend((uint)pixelBuffer[index], (uint)waterPixels[index], ratio);
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
                        shade += altitudeShade;

                        if(Settings.SHADE3D) {
                            pixelBuffer[index] = (int)Global.AddShade((uint)pixelBuffer[index], (int)(shade * 3), (int)(shade * 3), (int)(shade * 3));
                        } else {
                            pixelBuffer[index] = (int)Global.AddShade((uint)pixelBuffer[index], (int)(shade * 8), (int)(shade * 8), (int)(shade * 8));
                        }

                        

                    }
                }
            }
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
