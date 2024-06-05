using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media;
using Microsoft.Win32;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Input;
using Microsoft.VisualBasic.FileIO;

namespace Mcasaenk.UI.Canvas {
    public class ScreenshotManager {
        private TileMap tileMap;
        private Resolution resolution;
        private bool rotated;
        private Point Loc1;
        private Point Loc2;
        

        public readonly bool canResize;
        public ScreenshotManager(TileMap tileMap, Resolution resolution, bool canResize, Point startLocation = default) {
            this.tileMap = tileMap;
            this.resolution = resolution;
            this.canResize = canResize;
            {
                Loc1 = startLocation;
                Loc2 = startLocation.Add(new Point(resolution.X, resolution.Y));
            }
            rotated = false;
        }

        public Rect LocalRect(WorldPosition screen) { 
            return new Rect(screen.GetLocalPos(Loc1), screen.GetLocalPos(Loc2)); 
        }
        public Rect Rect() {
            return new Rect(Loc1, Loc2);
        }

        public void Rotate() {
            rotated = !rotated;

            var mid = Rect().Mid();
            var r = mid.Sub(Loc1);

            Loc1 = mid.Sub(new Point(r.Y, r.X));
            Loc2 = mid.Add(new Point(r.Y, r.X));

            ResizeCorner(Loc1);
        }
        public void ResizeCorner(Point p) {
            Loc1 = p;
            resolution.X = (int)Math.Abs(Loc1.X - Loc2.X);
            resolution.Y = (int)Math.Abs(Loc1.Y - Loc2.Y);
        }
        public void ResizeAxis(int a, bool x) {
            if(x) {
                Loc1.X = a;
                resolution.X = (int)Math.Abs(Loc1.X - Loc2.X);
            } else {
                Loc1.Y = a;
                resolution.Y = (int)Math.Abs(Loc1.Y - Loc2.Y);
            }
        }
        public void Move(Point byHow) {
            Loc1 = Loc1.Add(byHow);
            Loc2 = Loc2.Add(byHow);
        }
        public void Rebase(bool north, bool west) { 
            Point new1 = new Point(0, 0), new2 = new Point();

            if(north) {
                new1.Y = Math.Min(Loc1.Y, Loc2.Y);
                new2.Y = Math.Max(Loc1.Y, Loc2.Y);
            } else {
                new1.Y = Math.Max(Loc1.Y, Loc2.Y);
                new2.Y = Math.Min(Loc1.Y, Loc2.Y);
            }

            if(west) {
                new1.X = Math.Min(Loc1.X, Loc2.X);
                new2.X = Math.Max(Loc1.X, Loc2.X);
            } else {
                new1.X = Math.Max(Loc1.X, Loc2.X);
                new2.X = Math.Min(Loc1.X, Loc2.X);
            }

            Loc1 = new1;
            Loc2 = new2;
            resolution.X = (int)Math.Abs(Loc1.X - Loc2.X);
            resolution.Y = (int)Math.Abs(Loc1.Y - Loc2.Y);
        }




        public RenderTargetBitmap TakeScreenshot() {
            try {
                var Size = Rect().Size.AsPoint();

                var renderBitmap = new RenderTargetBitmap((int)Size.X, (int)Size.Y, 96, 96, PixelFormats.Pbgra32);

                var drawing = new DrawingVisual();
                RenderOptions.SetEdgeMode(drawing, EdgeMode.Aliased);
                using(DrawingContext graphics = drawing.RenderOpen()) {
                    int xoff = Global.Coord.absMod((int)Loc1.X, 512), zoff = Global.Coord.absMod((int)Loc1.Y, 512);
                    int stX = Global.Coord.fairDev((int)Loc1.X, 512), stZ = Global.Coord.fairDev((int)Loc1.Y, 512);
                    for(int x = stX; x <= Global.Coord.fairDev((int)Loc1.X + (int)Size.X, 512); x++) {
                        for(int z = stZ; z <= Global.Coord.fairDev((int)Loc1.Y + (int)Size.Y, 512); z++) {
                            var tile = tileMap.GetTile(new Point2i(x, z));
                            if(tile == null) continue;
                            if(tile.img == null) continue;
                            graphics.DrawImage(tileMap.GetTile(new Point2i(x, z)).img, new Rect((x - stX) * 512 - xoff, (z - stZ) * 512 - zoff, 512, 512));
                        }
                    }
                }
                renderBitmap.Render(drawing);

                return renderBitmap;
            }
            catch {
                MessageBox.Show("The image couldn't have been generated\nThis often occurs if the image size was too big");
                return null;
            }
        }

        public void TakeAndSaveScreenShot() {
            var saveFileDialog = new SaveFileDialog {
                Filter = "PNG Image|*.png",
                Title = "Save screenshot",
                FileName = $"screenshot{resolution.X}x{resolution.Y}"
            };

            if(saveFileDialog.ShowDialog() == true) {
                var encoder = new PngBitmapEncoder();
                var screenshot = this.TakeScreenshot();
                if(screenshot == null) return;
                encoder.Frames.Add(BitmapFrame.Create(screenshot));
                using(var fileStream = new FileStream(saveFileDialog.FileName, FileMode.Create)) {
                    encoder.Save(fileStream);
                }
            }
        }

        public Cursor MouseOverWhat(WorldPosition screen, Point mousePos) {
            int e = ScreenshotPainer.EdgeSize(screen.zoom);
            var p = new Point(e, e).Dev(2);
            var s = new Size(e, e);

            var LocNW = Rect().TopLeft;
            var Size = Rect().Size.AsPoint();

            if(new Rect(screen.GetLocalPos(LocNW).Sub(p), s).Contains(mousePos)) return Cursors.ScrollNW;
            if(new Rect(screen.GetLocalPos(LocNW.Add(new Point(Size.X, 0))).Sub(p), s).Contains(mousePos)) return Cursors.ScrollNE;
            if(new Rect(screen.GetLocalPos(LocNW.Add(new Point(0, Size.Y))).Sub(p), s).Contains(mousePos)) return Cursors.ScrollSW;
            if(new Rect(screen.GetLocalPos(LocNW.Add(new Point(Size.X, Size.Y))).Sub(p), s).Contains(mousePos)) return Cursors.ScrollSE;

            if(new Rect(screen.GetLocalPos(LocNW.Add(new Point(Size.X / 2, 0))).Sub(p), s).Contains(mousePos)) return Cursors.ScrollN;
            if(new Rect(screen.GetLocalPos(LocNW.Add(new Point(Size.X / 2, Size.Y))).Sub(p), s).Contains(mousePos)) return Cursors.ScrollS;
            if(new Rect(screen.GetLocalPos(LocNW.Add(new Point(0, Size.Y / 2))).Sub(p), s).Contains(mousePos)) return Cursors.ScrollW;
            if(new Rect(screen.GetLocalPos(LocNW.Add(new Point(Size.X, Size.Y / 2))).Sub(p), s).Contains(mousePos)) return Cursors.ScrollE;

            if(new Rect(screen.GetLocalPos(LocNW), Size.Mult(screen.zoom).AsSize()).Contains(mousePos)) return Cursors.Cross;

            return null;
        }
    }
}
