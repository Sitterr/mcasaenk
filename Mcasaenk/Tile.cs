using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Runtime.Intrinsics.Arm;
using System.Windows.Media.Media3D;
using System.Diagnostics;
using Mcasaenk.UI.Canvas;

namespace Mcasaenk {
    public class TileMap {
        private static Dictionary<Point2i, Tile> tiles = new();
        public static LimitedConcurrencyLevelTaskScheduler loading_pool = new LimitedConcurrencyLevelTaskScheduler(Settings.MAXCONCURRENCY);
        public static Tile GetTile(Point2i point, WorldPosition observer) {
            Tile tile;
            if(tiles.TryGetValue(point, out tile) == false) { 
                tile = new Tile(point);
                tiles.Add(point, tile);
            }
            tile.NewObserver(observer);
            return tile;
        }
    }

    public class Tile {
        public const int SIDE = 512;
        public const int SIZE = SIDE * SIDE;

        public List<WorldPosition> observers;

        public Point2i pos;
        public TileImage image;

        public bool Loaded { get; set; } // temp
        public bool Loading { get; set; } // temp
        public bool Queued { get; set; } // temp

        public Tile(Point2i position) {
            this.pos = position;
            image = new TileImage(this);
            observers = new List<WorldPosition>();
        }

        public void NewObserver(WorldPosition screen) {
            if(observers.Contains(screen) == false) observers.Add(screen);
        }
        public List<WorldPosition> GetObservers() {
            return observers;
        }

        public void Load() {
            Queued = true;
            var task = new Task(() => {
                try {
                    bool atleastone = false;
                    foreach(var screen in this.GetObservers()) {
                        if(screen.IsVisible(this)) {
                            atleastone = true;
                            break;
                        }
                    }
                    if(!atleastone) return;
                    Loading = true;

                    Task.Delay(2000).Wait();
                    image.GenerateForreal();

                    Loaded = true;
                }
                finally {
                    Queued = false;
                    Loading = false;
                }
            });

            task.Start(TileMap.loading_pool);
        }
    }

    public class TileImage {
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
            img = GenerateBitmap(RandomPixels());
            img.Freeze();
        }


        private static WriteableBitmap GenerateBitmap(byte[] pixels) {
            WriteableBitmap output = new WriteableBitmap(Tile.SIDE, Tile.SIDE, 96, 96, PixelFormats.Rgb24, null);
            
            int stride = (int)output.Width * (output.Format.BitsPerPixel / 8);
            output.WritePixels(new Int32Rect(0, 0, Tile.SIDE, Tile.SIDE), pixels, stride, 0);

            output.Freeze();
            return output;
        }
        private static byte[] RandomPixels() {
            byte[] pixels = new byte[Tile.SIZE * 3];
            byte r = (byte)Global.rand.Next(0, 256);
            byte g = (byte)Global.rand.Next(0, 256);
            byte b = (byte)Global.rand.Next(0, 256);

            for(int i = 0; i < Tile.SIZE; i++) {
                byte r1 = r, g1 = g, b1 = b;

                if(Global.rand.NextDouble() < 0.1) {
                    r1 = 55;
                    g1 = 55;
                    b1 = 55;
                }

                pixels[i * 3] = r1;
                pixels[i * 3 + 1] = b1;
                pixels[i * 3 + 2] = g1;
            }

            return pixels;
        }

    }
}
