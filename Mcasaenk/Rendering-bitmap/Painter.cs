﻿using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mcasaenk.Rendering;
using Mcasaenk.UI.Canvas;

namespace Mcasaenk.Rendering_bitmap {
    public abstract class Painter {

        private readonly DrawingGroup drawingGroup;
        public Painter() {
            drawingGroup = new DrawingGroup();
            //RenderOptions.SetEdgeMode(drawingGroup, EdgeMode.Aliased);
        }

        public Drawing GetDrawing() { return drawingGroup; }

        public void Update(WorldPosition wPos) {
            using var graphics = this.drawingGroup.Open();
            this.Render(graphics, wPos);
        }

        protected abstract void Render(DrawingContext graphics, WorldPosition screen);
    }

    public class ScenePainter : Painter {
        private BitmapDrawTileMap drawTileMap;
        private GenDataTileMap genTileMap;
        public void SetTileMap(BitmapDrawTileMap drawTileMap, GenDataTileMap genTileMap) {
            this.drawTileMap = drawTileMap;
            this.genTileMap = genTileMap;
        }

        protected override void Render(DrawingContext graphics, WorldPosition screen) {
            if(drawTileMap == null) return;
            graphics.PushTransform(new ScaleTransform(screen.zoom, screen.zoom));
            foreach(var pos in drawTileMap.GetVisibleTilesPositions(screen)) {
                Rect rect = new Rect(pos.X * 512 - screen.Start.X, pos.Z * 512 - screen.Start.Y, 512, 512);
                if(drawTileMap.GetTile(pos, out var img)) {
                    graphics.DrawImage(img, rect);
                } else if(genTileMap?.RegionExists(pos) == true) {
                    if(Global.App.Settings.UNLOADED) graphics.DrawImage(GetUnloadedOverlay(), rect);
                }

                if(Global.App.Settings.OVERLAYS) {
                    if(genTileMap != null) {
                        if(genTileMap.IsLoading(pos)) {
                            graphics.DrawImage(GetLoadingOverlay(), rect);
                        } else if(drawTileMap.IsLoading(pos)) {
                            graphics.DrawImage(GetDrawingOverlay(), rect);
                        }

                        if(genTileMap.GetShadeTile(pos)?.IsActive == true) {
                            graphics.DrawImage(GetRedOverlay(), rect);
                        }
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
                    RadiusX = 1.75,
                    RadiusY = 1.75,
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

    public class ScreenshotPainer : Painter {
        public static int EdgeSize(double zoom) {
            return (int)Math.Round(10 + zoom);
        }

        private Brush backBrush;
        private Pen greenPen, orangePen, yellowPen, redPen;

        private ScreenshotManager manager;

        private GenDataTileMap genTileMap;
        private BitmapDrawTileMap drawTileMap;

        public ScreenshotPainer() {
            greenPen = new Pen(new SolidColorBrush(Colors.Green), 2);
            orangePen = new Pen(new SolidColorBrush(Colors.Orange), 2);
            yellowPen = new Pen(new SolidColorBrush(Colors.LightGreen), 2);
            redPen = new Pen(new SolidColorBrush(Colors.Red), 2);

            backBrush = new SolidColorBrush(Global.FromArgb(0.15, Colors.White));
        }

        public void SetManager(BitmapDrawTileMap drawTileMap, GenDataTileMap genTileMap, ScreenshotManager manager) {
            this.drawTileMap = drawTileMap;
            this.genTileMap = genTileMap;
            this.manager = manager;
        }

        protected override void Render(DrawingContext graphics, WorldPosition screen) {
            if(manager == null) return;

            Pen outline = greenPen;
            var glrect = manager.AsRect();

            var state = manager.GetState(genTileMap);
            outline = state switch {
                ScreenshotManager.ConditionalState.ok => greenPen,
                ScreenshotManager.ConditionalState.shadesnotfinished => yellowPen,
                ScreenshotManager.ConditionalState.unloadedchunks => orangePen,
                ScreenshotManager.ConditionalState.invalid => redPen,
                _ => greenPen
            };

            var locrect = new Rect(screen.GetLocalPos(glrect.TopLeft), screen.GetLocalPos(glrect.BottomRight));
            graphics.DrawRectangle(backBrush, outline, locrect);

            if(manager.canResize) {
                int e = EdgeSize(screen.zoom);
                var p = new Point(e, e).Dev(2);
                var s = new Size(e, e);

                graphics.DrawRectangle(outline.Brush, null, new Rect(locrect.TopLeft.Sub(p), s));
                graphics.DrawRectangle(outline.Brush, null, new Rect(locrect.TopRight.Sub(p), s));
                graphics.DrawRectangle(outline.Brush, null, new Rect(locrect.BottomLeft.Sub(p), s));
                graphics.DrawRectangle(outline.Brush, null, new Rect(locrect.BottomRight.Sub(p), s));

                graphics.DrawRectangle(outline.Brush, null, new Rect(new Point(locrect.Left, locrect.Top + locrect.Height / 2).Sub(p), s));
                graphics.DrawRectangle(outline.Brush, null, new Rect(new Point(locrect.Right, locrect.Top + locrect.Height / 2).Sub(p), s));
                graphics.DrawRectangle(outline.Brush, null, new Rect(new Point(locrect.Left + locrect.Width / 2, locrect.Top).Sub(p), s));
                graphics.DrawRectangle(outline.Brush, null, new Rect(new Point(locrect.Left + locrect.Width / 2, locrect.Bottom).Sub(p), s));
            }
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
            double tx = Global.Coord.absMod(screen.Start.X, 16) * screen.zoom, tz = Global.Coord.absMod(screen.Start.Y, 16) * screen.zoom;

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
            double tx = Global.Coord.absMod(screen.Start.X, 512) * screen.zoom, tz = Global.Coord.absMod(screen.Start.Y, 512) * screen.zoom;
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
    class MapGridPainter : Painter {
        private Pen pen;
        public MapGridPainter() {
            pen = new Pen(new SolidColorBrush(Global.FromArgb(0.50, Colors.Yellow)), 1);
        }

        protected override void Render(DrawingContext graphics, WorldPosition screen) {
            int size = Global.Settings.MAPGRID switch {
                MapGridType.zoom0 => 128,
                MapGridType.zoom1 => 256,
                MapGridType.zoom2 => 512,
                MapGridType.zoom3 => 1024,
                _ => 0,
            };

            double tx = Global.Coord.absMod(screen.Start.X - size / 2, size) * screen.zoom, tz = Global.Coord.absMod(screen.Start.Y - size / 2, size) * screen.zoom;
            graphics.PushTransform(new TranslateTransform(-tx, -tz));

            for(int zz = 0; zz < screen.ScreenHeight + tz; zz += (int)(size * screen.zoom)) {
                graphics.DrawLine(pen, new Point(1, zz), new Point(tx + screen.ScreenWidth, zz));
            }
            for(int xx = 0; xx < screen.ScreenWidth + tx; xx += (int)(size * screen.zoom)) {
                graphics.DrawLine(pen, new Point(xx, 1), new Point(xx, 0 + tz + screen.ScreenHeight));
            }
        }
    }
    public class GridPainter2 : Painter {
        ChunkGridPainter chunkPainter;
        RegionGridPainter regionPainter;
        MapGridPainter mapPainter;

        public GridPainter2() {
            chunkPainter = new ChunkGridPainter();
            regionPainter = new RegionGridPainter();
            mapPainter = new MapGridPainter();
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

            if(Global.App.Settings.MAPGRID != MapGridType.None) {
                if(screen.ZoomScale >= -1) {
                    mapPainter.Update(screen);
                    graphics.DrawDrawing(mapPainter.GetDrawing());
                }
            }
        }
    }
    public class BackgroundPainter : Painter {
        private Brush solidBrush, checkerBrush;
        public BackgroundPainter() {
            {
                if(Global.App.Settings.CONTRAST < 0.90) solidBrush = new SolidColorBrush(Color.FromRgb(15, 15, 15));
                else solidBrush = new SolidColorBrush(Colors.Black);
                solidBrush.Freeze();
            }

            {
                const double squareSize = 32;
                Brush darkBrush = new SolidColorBrush(Color.FromRgb(10, 10, 10));
                Brush lightBrush = new SolidColorBrush(Color.FromRgb(15, 15, 15));

                DrawingGroup checkerDrawingGroup = new DrawingGroup();
                for(int y = 0; y < 2; y++) {
                    for(int x = 0; x < 2; x++) {
                        var brush = ((x + y) % 2 == 0) ? darkBrush : lightBrush;
                        var squareDrawing = new GeometryDrawing(brush, null, new RectangleGeometry(new Rect(x * squareSize, y * squareSize, squareSize, squareSize)));
                        checkerDrawingGroup.Children.Add(squareDrawing);
                    }
                }

                checkerBrush = new DrawingBrush(checkerDrawingGroup) {
                    Viewport = new Rect(0, 0, squareSize * 2, squareSize * 2),
                    ViewportUnits = BrushMappingMode.Absolute,
                    TileMode = TileMode.Tile
                };
                checkerBrush.Freeze();
            }

        }

        protected override void Render(DrawingContext graphics, WorldPosition screen) {
            switch(Global.App.Settings.BACKGROUND) {
                case BackgroundType.None:
                    graphics.DrawRectangle(solidBrush, null, new Rect(0, 0, screen.ScreenWidth, screen.ScreenHeight));
                    break;
                case BackgroundType.Checker:
                    graphics.DrawRectangle(checkerBrush, null, new Rect(0, 0, screen.ScreenWidth, screen.ScreenHeight));
                    break;
            }
        }
    }
}
