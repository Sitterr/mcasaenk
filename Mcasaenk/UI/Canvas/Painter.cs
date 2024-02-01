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
        private TileMap tileMap;
        public void SetTileMap(TileMap tileMap) {
            this.tileMap = tileMap;
        }

        protected override void Render(DrawingContext graphics, WorldPosition screen) {
            if(tileMap == null) return;
            graphics.PushTransform(new ScaleTransform(screen.zoom, screen.zoom));
            foreach(var pos in screen.GetVisibleTilePositions()) {
                var tile = tileMap.GetTile(pos, screen);
                Rect rect = new Rect(tile.pos.X * 512 - screen.coord.X, tile.pos.Z * 512 - screen.coord.Y, 512, 512);
                if(tile.GetImage() != null) {
                    graphics.DrawImage(tile.GetImage(), rect);
                }
                if(tile.Loading) {
                    graphics.DrawImage(GetLoadingOverlay(), rect);
                }
            }
        }
    

        private ImageSource _loadingOverlay = null;
        private ImageSource GetLoadingOverlay() {
            _loadingOverlay ??= GenerateLoadingBitmap();
            return _loadingOverlay;
        }

        private static ImageSource GenerateLoadingBitmap() {
            Drawing generateBrushDrawing() {
                Pen pen1 = new Pen(new SolidColorBrush(Global.ColorPallete.Pallete.s0), 3);
                Pen pen2 = new Pen(new SolidColorBrush(Global.ColorPallete.Pallete.s8), 3);
                pen1.Freeze(); pen2.Freeze();
                DrawingGroup brushDrawing = new DrawingGroup();
                RenderOptions.SetEdgeMode(brushDrawing, EdgeMode.Aliased);
                using(DrawingContext graphics = brushDrawing.Open()) {
                    for(int i = 0; i < 3; i++) {
                        for(int j = 0; j < 3; j++) {
                            Pen pen = pen1;
                            if(i % 2 == j % 2) pen = pen2;
                            graphics.DrawLine(pen, new Point(i * 32 + 6, j * 32 + 6), new Point(i * 32 + 22, j * 32 + 22));
                        }
                    }


                }
                return brushDrawing;
            }

            DrawingBrush brush = new DrawingBrush();
            Rect tileRect = new Rect(0, 0, 32 * 3, 32 * 3);
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

        const int dashtickness = 4;
        const int dashsize = 32;
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

            Pen pen = screen.ZoomScale < 0 ? linePen : dashPen;

            for(int zz = 1; zz < screen.ScreenHeight + tz; zz += (int)(512 * screen.zoom)) {
                graphics.DrawLine(pen, new Point(dashsize / 2 + 1, zz - 1), new Point(tx + screen.ScreenWidth, zz - 1));
            }
            for(int xx = 1; xx < screen.ScreenWidth + tx; xx += (int)(512 * screen.zoom)) {
                graphics.DrawLine(pen, new Point(xx - 1, dashsize / 2 + 1), new Point(xx - 1, 0 + tz + screen.ScreenHeight));
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
            if(Settings.CHUNKGRID) {
                if(screen.ZoomScale >= 0) {
                    chunkPainter.Update(screen);
                    graphics.DrawDrawing(chunkPainter.GetDrawing());
                }
            }

            if(Settings.REGIONGRID) {
                regionPainter.Update(screen);
                graphics.DrawDrawing(regionPainter.GetDrawing());
            }
        }
    }
    public class BackgroundPainter : Painter {
        private Brush brush;
        public BackgroundPainter() {
            //brush = new LinearGradientBrush(new GradientStopCollection(new[] { new GradientStop(Colors.Yellow, 0), new GradientStop(Colors.Blue, 1) }));
            brush = new SolidColorBrush(Global.ColorPallete.Pallete.s2);
            brush.Freeze();
        }

        protected override void Render(DrawingContext graphics, WorldPosition screen) {
            graphics.DrawRectangle(brush, null, new Rect(0, 0, screen.ScreenWidth, screen.ScreenHeight));
        }
    }
}
