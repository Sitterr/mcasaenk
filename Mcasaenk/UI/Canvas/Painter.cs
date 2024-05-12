using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Xps;

namespace Mcasaenk.UI.Canvas {
    public abstract class Painter {

        private readonly DrawingGroup drawingGroup;
        public Painter() {
            drawingGroup = new DrawingGroup();
           //RenderOptions.SetEdgeMode(drawingGroup, EdgeMode.Aliased);
        }

        public Drawing GetDrawing() {  return drawingGroup; }

        public void Update(WorldPosition wPos) {
            using var graphics = this.drawingGroup.Open();
            this.Render(graphics, wPos);
        }

        protected abstract void Render(DrawingContext graphics, WorldPosition screen);
    }


    public class ScenePainter : Painter {
        private TileMap tileMap = null;
        public void SetTileMap(TileMap tileMap) {
            this.tileMap = tileMap;
        }

        protected override void Render(DrawingContext graphics, WorldPosition screen) {
            if(tileMap == null) return;
            graphics.PushTransform(new ScaleTransform(screen.zoom, screen.zoom));
            foreach(var pos in screen.GetVisibleTilePositions()) {
                var tile = tileMap.GetTile(pos);
                if(tile == null) continue;
                Rect rect = new Rect(tile.pos.X * 512 - screen.coord.X, tile.pos.Z * 512 - screen.coord.Y, 512, 512);
                if(tile.img != null) {
                    graphics.DrawImage(tile.img, rect);
                } else {
                    if(Global.App.Settings.UNLOADED) graphics.DrawImage(GetUnloadedOverlay(), rect);
                }

                if(Global.App.Settings.OVERLAYS) {
                    if(tileMap.generateTilePool.IsLoading(tile)) {
                        graphics.DrawImage(GetLoadingOverlay(), rect);
                    } else if(tileMap.drawTilePool.IsLoading(tile)) {
                        graphics.DrawImage(GetDrawingOverlay(), rect);
                    }

                    if(tile.shade.IsActive) {
                        graphics.DrawImage(GetRedOverlay(), rect);
                    }
                }
            }
        }
    

        private ImageSource _unloadedOverlay = null;
        private ImageSource GetUnloadedOverlay() {
            _unloadedOverlay ??= GenerateUnloaded();
            return _unloadedOverlay;
        }
        private static ImageSource GenerateUnloaded() {
            Drawing generateBrushDrawing() {
                Pen pen1 = new Pen(new SolidColorBrush(Color.FromRgb(173, 216, 230)), 24);
                Pen pen2 = new Pen(new SolidColorBrush(Color.FromRgb(90, 90, 90)), 24);
                pen1.Freeze(); pen2.Freeze();
                DrawingGroup brushDrawing = new DrawingGroup();
                RenderOptions.SetEdgeMode(brushDrawing, EdgeMode.Aliased);
                using(DrawingContext graphics = brushDrawing.Open()) {
                    for(int i = 0; i < 4; i++) {
                        for(int j = 0; j < 4; j++) {
                            Pen pen = pen1;
                            if(i % 2 == j % 2) pen = pen2;
                            int q = 24;
                            graphics.DrawLine(pen, new Point(i * 128 + q, j * 128 + q), new Point(i * 128 + (128 - 2 * q), j * 128 + (128 - 2 * q)));
                        }
                    }


                }
                return brushDrawing;
            }

            DrawingBrush brush = new DrawingBrush();
            Rect tileRect = new Rect(0, 0, 128 * 4, 128 * 4);
            brush.Drawing = generateBrushDrawing();
            brush.Stretch = Stretch.None;
            brush.TileMode = TileMode.Tile;
            brush.Viewport = tileRect;
            brush.ViewportUnits = BrushMappingMode.Absolute;
            brush.Freeze();

            RenderTargetBitmap output = new RenderTargetBitmap(512, 512, 96, 96, PixelFormats.Default);

            DrawingVisual drawingVisual = new DrawingVisual();
            using(var graphics = drawingVisual.RenderOpen()) {
                graphics.DrawRectangle(brush, null, new Rect(0, 0, 512, 512));
            }
            output.Render(drawingVisual);
            output.Freeze();

            return output;
        }


        private ImageSource _loadingOverlay = null;
        private ImageSource GetLoadingOverlay() {
            _loadingOverlay ??= GenerateColorOverlay(Colors.Transparent, Color.FromRgb(200, 200, 200), 0.65);
            return _loadingOverlay;
        }
        private static ImageSource GenerateColorOverlay(Color centerColor, Color outerColor, double alpha) {
            RenderTargetBitmap output = new RenderTargetBitmap(512, 512, 96, 96, PixelFormats.Default);

            DrawingVisual drawingVisual = new DrawingVisual();
            using(var graphics = drawingVisual.RenderOpen()) {
                var radient = new RadialGradientBrush(centerColor, Global.FromArgb(alpha, outerColor)) {
                    Center = new Point(0.5, 0.5),
                    RadiusX = 1.75, RadiusY = 1.75,
                    GradientOrigin = new Point(0.5, 0.5),
                };
                graphics.DrawRectangle(radient, null, new Rect(0, 0, 512, 512));
            }
            output.Render(drawingVisual);
            output.Freeze();

            return output;
        }

        private ImageSource _drawingOverlay = null;
        private ImageSource GetDrawingOverlay() {
            _drawingOverlay ??= GenerateColorOverlay(Colors.Transparent, Colors.GreenYellow, 0.65);
            return _drawingOverlay;
        }


        private ImageSource _redOverlay = null;
        private ImageSource GetRedOverlay() {
            _redOverlay ??= GenerateColorOverlay(Colors.Transparent, Colors.Red, 0.65);
            return _redOverlay;
        }
    }

    class ChunkGridPainter : Painter {
        private Pen pen;
        private DrawingBrush cached_chunkBrush;
        private double cached_zoom = -1;

        const double opacity = 0.5;

        public ChunkGridPainter() {
            pen = new Pen(new SolidColorBrush(Global.FromArgb(opacity, Colors.LightGray)), 1);
        }

        protected override void Render(DrawingContext graphics, WorldPosition screen) {
            double tx = Global.Coord.absMod(screen.coord.X, 16) * screen.zoom, tz = Global.Coord.absMod(screen.coord.Y, 16) * screen.zoom;

            graphics.PushTransform(new TranslateTransform(-tx, -tz));
            graphics.DrawRectangle(GetChunkBrush(screen.zoom), null, new Rect(0, 0, screen.ScreenWidth + tx, screen.ScreenHeight + tz));
        }


        private DrawingGroup GenerateChunkDrawing(Rect rect) {
            DrawingGroup drawing = new DrawingGroup();

            using(DrawingContext graphics = drawing.Open()) {
                graphics.DrawRectangle(null, pen, rect);
            }
            return drawing;
        }
        private DrawingBrush GetChunkBrush(double zoom) {
            if(cached_zoom != zoom) {

                cached_chunkBrush = new DrawingBrush();
                Rect tileRect = new Rect(0, 0, 16 * zoom, 16 * zoom);
                cached_chunkBrush.Drawing = GenerateChunkDrawing(tileRect);
                cached_chunkBrush.Stretch = Stretch.None;
                cached_chunkBrush.TileMode = TileMode.Tile;
                cached_chunkBrush.Viewport = tileRect;
                cached_chunkBrush.ViewportUnits = BrushMappingMode.Absolute;
                cached_chunkBrush.Freeze();

                cached_zoom = zoom;
            }
            return cached_chunkBrush;
        }
    }
    class RegionGridPainter : Painter {
        private Pen linePen, dashPen;

        const int dashtickness = 2;
        const int dashsize = 32, linesize = 0;
        const double opacity = 0.75;

        public RegionGridPainter() {
            linePen = new Pen(new SolidColorBrush(Global.FromArgb(opacity, Colors.White)), 1);
            dashPen = new Pen(new SolidColorBrush(Global.FromArgb(opacity, Colors.White)), dashtickness) {
                DashStyle = new DashStyle(new double[] { dashsize / dashtickness }, 0),
                DashCap = PenLineCap.Flat,
                StartLineCap = PenLineCap.Flat,
            };
        }

        protected override void Render(DrawingContext graphics, WorldPosition screen) {
            double tx = Global.Coord.absMod(screen.coord.X, 512) * screen.zoom, tz = Global.Coord.absMod(screen.coord.Y, 512) * screen.zoom;
            graphics.PushTransform(new TranslateTransform(-tx, -tz));

            Pen pen = Global.App.Settings.REGIONGRID == RegionGridType.Straight ? linePen : screen.ZoomScale < 0 ? linePen : dashPen;
            int pensize = pen == linePen ? linesize : dashsize;

            for(int zz = 0; zz < screen.ScreenHeight + tz; zz += (int)(512 * screen.zoom)) {
                graphics.DrawLine(pen, new Point(pensize / 2 + 1, zz), new Point(tx + screen.ScreenWidth, zz));
            }
            for(int xx = 0; xx < screen.ScreenWidth + tx; xx += (int)(512 * screen.zoom)) {
                graphics.DrawLine(pen, new Point(xx, pensize / 2 + 1), new Point(xx, 0 + tz + screen.ScreenHeight));
            }
        }
    }
    public class GridPainter2 : Painter {
        ChunkGridPainter chunkPainter;
        RegionGridPainter regionPainter;

        public GridPainter2() {
            chunkPainter = new ChunkGridPainter();
            regionPainter = new RegionGridPainter();
        }
    
        protected override void Render(DrawingContext graphics, WorldPosition screen) {
            if(Global.App.Settings.CHUNKGRID == ChunkGridType.Straight) {
                if(screen.ZoomScale >= 0) {
                    chunkPainter.Update(screen);
                    graphics.DrawDrawing(chunkPainter.GetDrawing());
                }
            }

            if(Global.App.Settings.REGIONGRID != RegionGridType.None) {
                regionPainter.Update(screen);
                graphics.DrawDrawing(regionPainter.GetDrawing());
            }
        }
    }
    public class BackgroundPainter : Painter {
        private Brush solidColorBrush;
        public BackgroundPainter() {
            //brush = new LinearGradientBrush(new GradientStopCollection(new[] { new GradientStop(Colors.Yellow, 0), new GradientStop(Colors.Blue, 1) }));
            if(Global.App.Settings.CONTRAST < 0.90) solidColorBrush = new SolidColorBrush(Color.FromRgb(15, 15, 15));
            else solidColorBrush = new SolidColorBrush(Colors.Black);
            solidColorBrush.Freeze();
        }

        protected override void Render(DrawingContext graphics, WorldPosition screen) {
            switch(Global.App.Settings.BACKGROUND) {
                case BackgroundType.None:
                    graphics.DrawRectangle(solidColorBrush, null, new Rect(0, 0, screen.ScreenWidth, screen.ScreenHeight));
                    break;
            }
        }
    }
}
