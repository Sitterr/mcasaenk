using System.Buffers;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mcasaenk.Nbt;
using Mcasaenk.Rendering;
using Mcasaenk.UI.Canvas;

namespace Mcasaenk.Rendering_bitmap {
    public class BitmapDrawTileMap : DrawGroupTileMap<WriteableBitmap> {
        private readonly ArrayPool<uint> pixelPool;

        public BitmapDrawTileMap(GenDataTileMap genTilemap, BitmapDrawTileMap oldTilemap) : base(genTilemap, 1, 1, true, oldTilemap) {
            int maxConcurrency = Global.Settings.DRAWMAXCONCURRENCY;
            pixelPool = ArrayPool<uint>.Create(512 * 512, maxConcurrency);

            SetQueuer(new ObserverTaskTileMapQueuer<WriteableBitmap>(this, maxConcurrency));
        }
        public bool IsLoading(Point2i p) => ((ObserverTaskTileMapQueuer<WriteableBitmap>)queuer).IsLoading(p);
        public bool IsQueued(Point2i p) => ((ObserverTaskTileMapQueuer<WriteableBitmap>)queuer).IsQueued(p);

        public override void DoVisible(KeyValuePair<string, WorldPosition> visiblescreen, KeyValuePair<string, WorldPosition>[] movingextras, bool quickscan) {
            base.DoVisible(visiblescreen, [], quickscan);
        }
        protected override WriteableBitmap __Do(Point2i pos, WriteableBitmap bitmap) {
            uint[] pixels = pixelPool.Rent(512 * 512);

            GenData[,] neighbours = new GenData[3, 3];
            neighbours[1, 1] = gentilemap.GetTile(pos);
            if(Global.App.Colormap.TintManager.GetBlendingTints().Any() || Global.Settings.OCEAN_DEPTH_BLENDING > 1) {
                for(int i = -1; i <= 1; i++) { // biome blend
                    for(int j = -1; j <= 1; j++) {
                        neighbours[i + 1, j + 1] = gentilemap.GetTile(pos + new Point2i(i, j));
                    }
                }
            } else if(Global.Settings.SHADETYPE == ShadeType.jmap) {
                Point2i p = Global.Settings.Jmap_MAP_DIRECTION switch {
                    Direction.North => new Point2i(0, -1),
                    Direction.South => new Point2i(0, 1),
                    Direction.East => new Point2i(1, 0),
                    Direction.West => new Point2i(-1, 0),
                };
                neighbours[p.X + 1, p.Z + 1] = gentilemap.GetTile(pos + p);
            }

            var tempgen = gentilemap.GetTile(pos).GetTempInstance();
            TileDraw.FillPixels(pixels, Global.App.Colormap, tempgen, neighbours);
            tempgen.DisposeTemporal();

            Global.App.Dispatcher.Invoke(() => {
                bitmap.WritePixels(new Int32Rect(0, 0, 512, 512), pixels, 4 * 512, 0);
            });
            return bitmap;
        }

        public override void OnScaleChange(double scale) {
            this.scale = 1;
        }

        protected override WriteableBitmap CreateTile() {
            WriteableBitmap bitmap = null;
            Global.App.Dispatcher.Invoke(() => {
                bitmap = new WriteableBitmap(512, 512, 96, 96, PixelFormats.Bgra32, null);
            });
            return bitmap;
        }
        protected override void DisposeTile(WriteableBitmap bitmap) { }
    }

    public class BitmapClusterScreenshotTaker : ScreenshotTaker {
        private readonly BitmapDrawTileMap drawTileMap;
        private WorldPosition frame;
        private bool rotate;
        public BitmapClusterScreenshotTaker(BitmapDrawTileMap drawTileMap, WorldPosition frame, bool rotate) {
            this.drawTileMap = drawTileMap;
            this.frame = frame;
            this.rotate = rotate;
        }

        private BitmapSource Render() {
            //var Size = new Size(frame.ScreenWidth, frame.ScreenHeight);
            var NW = frame.Start;

            var renderBitmap = new RenderTargetBitmap(frame.ScreenWidth, frame.ScreenHeight, 96, 96, PixelFormats.Pbgra32);
            if(drawTileMap != null) {
                var drawing = new DrawingVisual();

                // ?!??!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!!?!??!?!?!?!??!???!?!?????!?!?!
                // ?!??!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!!?!??!?!?!?!??!???!?!?????!?!?!
                // ?!??!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!!?!??!?!?!?!??!???!?!?????!?!?!
                {
                    /* totally not bugged as fuck
                            RenderOptions.SetBitmapScalingMode(drawing, BitmapScalingMode.NearestNeighbor);
                            RenderOptions.SetEdgeMode(drawing, EdgeMode.Aliased);
                     */

                    drawing.GetType().GetProperty("VisualEdgeMode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(drawing, EdgeMode.Aliased);
                    drawing.GetType().GetProperty("VisualBitmapScalingMode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(drawing, BitmapScalingMode.NearestNeighbor);
                }
                // ?!??!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!!?!??!?!?!?!??!???!?!?????!?!?!
                // ?!??!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!!?!??!?!?!?!??!???!?!?????!?!?!
                // ?!??!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!!?!??!?!?!?!??!???!?!?????!?!?!

                using(DrawingContext graphics = drawing.RenderOpen()) {
                    var scaleTransform = new ScaleTransform(frame.zoom, frame.zoom);
                    graphics.PushTransform(scaleTransform);

                    int xoff = Global.Coord.absMod((int)NW.X, 512), zoff = Global.Coord.absMod((int)NW.Y, 512);
                    int stX = (int)Math.Floor(NW.X / 512), stZ = (int)Math.Floor(NW.Y / 512);
                    for(int x = stX; x <= (int)Math.Floor((frame.Start.X + frame.Width) / 512); x++) {
                        for(int z = stZ; z <= (int)Math.Floor((frame.Start.Y + frame.Height) / 512); z++) {
                            if(drawTileMap.GetTile(new Point2i(x, z), out var img)) {
                                graphics.DrawImage(img, new Rect((x - stX) * 512 - xoff, (z - stZ) * 512 - zoff, 512, 512));
                            }
                        }
                    }
                }
                renderBitmap.Render(drawing);
            }

            if(rotate) return new TransformedBitmap(renderBitmap, new RotateTransform(-90));
            return renderBitmap;
        }

        public BitmapSource TakeScreenshotAsImage() {
            return this.Render();
        }

        public CompoundTag_Allgemein TakeScreenshotAsMap(Dimension dim, int version, ColorApproximationAlgorithm coloralgo) {
            uint[] pixels = new uint[16384];
            var screenshot = this.Render();
            screenshot.CopyPixels(pixels, 512, 0);

            return NBTBlueprints.CreateMapScreenshot(pixels, frame, dim, version, coloralgo);
        }
    }
}
