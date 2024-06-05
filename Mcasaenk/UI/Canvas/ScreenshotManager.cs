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

namespace Mcasaenk.UI.Canvas {
    public class ScreenshotManager {
        private TileMap tileMap;
        private Resolution resolution;
        private bool canresize;

        public ScreenshotManager(TileMap tileMap, Resolution resolution, bool canResize, Point startLocation = default) {
            this.tileMap = tileMap;
            this.resolution = resolution;
            this.canresize = canResize;
            if(startLocation != default) this.Location = startLocation;
            rotated = false;
        }

        private bool rotated;

        public Point Location;
        public Point Size {
            get {
                if(rotated) return new Point(resolution.Y, resolution.X);
                else return new Point(resolution.X, resolution.Y);
            }
        }
        public bool CanResize { get => canresize; }
        public Resolution Resolution { get => resolution; }

        public void Rotate() {
            var mid = Location.Add(Size.Dev(2));
            var diff = mid.Sub(Location);
            Location = mid.Sub(new Point(diff.Y, diff.X));

            rotated = !rotated;
        }

        public RenderTargetBitmap TakeScreenshot() {
            try {
                var renderBitmap = new RenderTargetBitmap((int)Size.X, (int)Size.Y, 96, 96, PixelFormats.Pbgra32);

                var drawing = new DrawingVisual();
                RenderOptions.SetEdgeMode(drawing, EdgeMode.Aliased);
                using(DrawingContext graphics = drawing.RenderOpen()) {
                    int xoff = Global.Coord.absMod((int)Location.X, 512), zoff = Global.Coord.absMod((int)Location.Y, 512);
                    int stX = Global.Coord.fairDev((int)Location.X, 512), stZ = Global.Coord.fairDev((int)Location.Y, 512);
                    for(int x = stX; x <= Global.Coord.fairDev((int)Location.X + (int)Size.X, 512); x++) {
                        for(int z = stZ; z <= Global.Coord.fairDev((int)Location.Y + (int)Size.Y, 512); z++) {
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

            if(new Rect(screen.GetLocalPos(Location).Sub(p), s).Contains(mousePos)) return Cursors.ScrollNW;
            if(new Rect(screen.GetLocalPos(Location.Add(new Point(Size.X, 0))).Sub(p), s).Contains(mousePos)) return Cursors.ScrollNE;
            if(new Rect(screen.GetLocalPos(Location.Add(new Point(0, Size.Y))).Sub(p), s).Contains(mousePos)) return Cursors.ScrollSW;
            if(new Rect(screen.GetLocalPos(Location.Add(new Point(Size.X, Size.Y))).Sub(p), s).Contains(mousePos)) return Cursors.ScrollSE;

            if(new Rect(screen.GetLocalPos(Location.Add(new Point(Size.X / 2, 0))).Sub(p), s).Contains(mousePos)) return Cursors.ScrollN;
            if(new Rect(screen.GetLocalPos(Location.Add(new Point(Size.X / 2, Size.Y))).Sub(p), s).Contains(mousePos)) return Cursors.ScrollS;
            if(new Rect(screen.GetLocalPos(Location.Add(new Point(0, Size.Y / 2))).Sub(p), s).Contains(mousePos)) return Cursors.ScrollW;
            if(new Rect(screen.GetLocalPos(Location.Add(new Point(Size.X, Size.Y / 2))).Sub(p), s).Contains(mousePos)) return Cursors.ScrollE;

            if(new Rect(screen.GetLocalPos(Location), Size.Mult(screen.zoom).AsSize()).Contains(mousePos)) return Cursors.Cross;

            return null;
        }
    }
}
