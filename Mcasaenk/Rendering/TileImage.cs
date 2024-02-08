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
            var pixels = HardGenerate();
            img = GenerateBitmap(pixels);
            PoolHandler.pixelBuffer.Return(pixels, true);
        }

        private int[] HardGenerate() {
            var chunks = RegionReader.ReadAnvilFileWithUnmanaged(tile.GetOrigin().dimension.GetRegionPath(tile.pos));

            int[] pixelBuffer = PoolHandler.pixelBuffer.Rent(512 * 512);
            int[] waterPixels = PoolHandler.waterPixels.Rent(512 * 512);
            short[] terrainHeights = PoolHandler.terrainHeights.Rent(512 * 512);
            short[] waterHeights = PoolHandler.waterHeights.Rent(512 * 512);

            for(int zz = 0; zz < 32; zz++) {
                for(int xx = 0; xx < 32; xx++) {
                    ChunkRenderer.DrawChunk(chunks[zz * 32 + xx], xx * 16, zz * 16, pixelBuffer, waterPixels, terrainHeights, waterHeights, 319);

                    chunks[zz * 32 + xx]?.Dispose();
                }
            }

            PoolHandler.waterPixels.Return(waterPixels, true);
            PoolHandler.terrainHeights.Return(terrainHeights, true);
            PoolHandler.waterHeights.Return(waterHeights, true);

            return pixelBuffer;
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
