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
        }

        public Drawing GetDrawing() {  return drawingGroup; }

        public void Update(WorldPosition wPos) {
            using var graphics = drawingGroup.Open();
            this.Render(graphics, wPos);
        }

        protected abstract void Render(DrawingContext graphics, WorldPosition screen);
    }


    public class ScenePainter : Painter {
        protected override void Render(DrawingContext graphics, WorldPosition screen) {
            graphics.PushTransform(new ScaleTransform(screen.zoom, screen.zoom));
            DrawImage(graphics, screen);
        }

        void DrawImage(DrawingContext graphics, WorldPosition screen) {
            ImageSource img;
            foreach(var tile in screen.GetVisibleTiles()) {
                Rect rect = new Rect(tile.pos.X * 512 - screen.position.X, tile.pos.Z * 512 - screen.position.Z, 512, 512);
                img = tile.image.GetImage();
                if(img == null) continue;
                graphics.DrawImage(img, rect);
            }
        }

    }

    class ChunkGridPainter : Painter {
        private Pen pen;
        private DrawingBrush cached_chunkBrush;
        private float cached_zoom = -1;

        public ChunkGridPainter() {
            pen = new Pen(new SolidColorBrush(Colors.LightGray), 1);
        }

        protected override void Render(DrawingContext graphics, WorldPosition screen) {
            float tx = Global.Coord.absMod(screen.position.X, 16) * screen.zoom, tz = Global.Coord.absMod(screen.position.Z, 16) * screen.zoom;

            graphics.PushTransform(new TranslateTransform(-tx, -tz));
            graphics.DrawRectangle(GetChunkBrush(screen.zoom), null, new Rect(0, 0, screen.w + tx, screen.h + tz));
        }


        private DrawingGroup GenerateChunkDrawing(Rect rect) {
            DrawingGroup drawing = new DrawingGroup();

            using(DrawingContext graphics = drawing.Open()) {
                graphics.DrawRectangle(null, pen, rect);
            }
            return drawing;
        }
        private DrawingBrush GetChunkBrush(float zoom) {
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
        private Pen linePen, dashPen, usedPen;
        private DrawingBrush cached_chunkBrush;
        private float cached_zoom = -1;

        const int dashsize = 4;

        public RegionGridPainter() {
            usedPen = new Pen(null, 0);
            linePen = new Pen(new SolidColorBrush(Colors.White), 1);
            dashPen = new Pen(new SolidColorBrush(Colors.White), 3) {
                DashStyle = new DashStyle(new double[] { dashsize, dashsize }, 0),
            };
        }

        protected override void Render(DrawingContext graphics, WorldPosition screen) {
            float tx = Global.Coord.absMod(screen.position.X, 512) * screen.zoom, tz = Global.Coord.absMod(screen.position.Z, 512) * screen.zoom;

            graphics.PushTransform(new TranslateTransform(-tx - (int)Math.Ceiling(usedPen.Thickness / 2), -tz - (int)Math.Ceiling(usedPen.Thickness / 2)));
            graphics.DrawRectangle(GetRegionBrush(screen), null, new Rect(0, 0, screen.w + tx, screen.h + tz));
        }


        private DrawingGroup GenerateRegionDrawing(Rect rect, int zoomScale) {
            DrawingGroup drawing = new DrawingGroup();
            //RenderOptions.SetBitmapScalingMode(drawing, BitmapScalingMode.NearestNeighbor);

            using(DrawingContext graphics = drawing.Open()) {
                if(zoomScale < 0) {
                    graphics.DrawRectangle(null, linePen, rect);
                    usedPen = linePen;
                } else if(zoomScale >= 0) {
                    graphics.DrawLine(dashPen, new Point(rect.Left, rect.Top), new Point(rect.Right, rect.Top));
                    graphics.DrawLine(dashPen, new Point(rect.Left, rect.Top), new Point(rect.Left, rect.Bottom));
                    usedPen = dashPen;
                }
            }

            return drawing;
        }
        private DrawingBrush GetRegionBrush(WorldPosition screen) {
            if(cached_zoom != screen.zoom) {

                cached_chunkBrush = new DrawingBrush();
                Rect tileRect = new Rect(0, 0, 512 * screen.zoom, 512 * screen.zoom);
                cached_chunkBrush.Drawing = GenerateRegionDrawing(tileRect, screen.GetZoomScale());
                cached_chunkBrush.TileMode = TileMode.Tile;
                cached_chunkBrush.Viewport = tileRect;
                cached_chunkBrush.ViewportUnits = BrushMappingMode.Absolute;
                cached_chunkBrush.Freeze();

                cached_zoom = screen.zoom;
            }
            return cached_chunkBrush;
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
                if(screen.GetZoomScale() >= 0) {
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
            graphics.DrawRectangle(brush, null, new Rect(0, 0, screen.w, screen.h));
        }
    }
}
