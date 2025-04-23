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
using System.Reflection;
using Mcasaenk.Nbt;
using System.IO.Compression;
using System.Xml.Serialization;

namespace Mcasaenk.UI.Canvas {
    public class ScreenshotManager {
        public readonly TileMap tileMap;
        private Resolution resolution;
        private ResolutionScale scale;
        private bool rotated;
        private Point Loc1;
        private Point Loc2;
        

        public readonly bool canResize;
        public ScreenshotManager(TileMap tileMap, Resolution resolution, ResolutionScale scale, bool canResize, Point startLocation = default) {
            this.tileMap = tileMap;
            this.resolution = resolution;
            this.scale = scale;
            this.canResize = canResize;
            {
                Loc1 = startLocation;
                Loc2 = startLocation.Add(new Point(resolution.X, resolution.Y).Dev(scale.Scale)).Floor();
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

        }
        public void Rescale() {
            var mid = Rect().Mid();

            int x = (int)(resolution.X / scale.Scale), y = (int)(resolution.Y / scale.Scale);
            if(rotated) (x, y) = (y, x);

            Loc1 = mid.Sub(new Point(x, y).Dev(2)).Floor();
            Loc2 = mid.Add(new Point(x, y).Dev(2)).Floor();
        }
        public void ResizeCorner(Point p) {
            Loc1 = p;
            resolution.X = (int)(Math.Abs(Loc1.X - Loc2.X) * scale.Scale);
            resolution.Y = (int)(Math.Abs(Loc1.Y - Loc2.Y) * scale.Scale);
        }
        public void ResizeAxis(int a, bool x) {
            if(x) {
                Loc1.X = a;
                resolution.X = (int)(Math.Abs(Loc1.X - Loc2.X) * scale.Scale);
            } else {
                Loc1.Y = a;
                resolution.Y = (int)(Math.Abs(Loc1.Y - Loc2.Y) * scale.Scale);
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
            //resolution.X = (int)(Math.Abs(Loc1.X - Loc2.X) * scale.Scale);
            //resolution.Y = (int)(Math.Abs(Loc1.Y - Loc2.Y) * scale.Scale);
        }




        private BitmapSource TakeScreenshot(BitmapScalingMode scalingMode) {
            try {
                var Size = Rect().Size.AsPoint();
                var NW = Rect().TopLeft;

                var (rx, ry) = (resolution.X, resolution.Y);
                if(rotated) (ry, rx) = (resolution.X, resolution.Y);
                var renderBitmap = new RenderTargetBitmap(rx, ry, 96, 96, PixelFormats.Pbgra32);
                if(tileMap != null) {
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
                        drawing.GetType().GetProperty("VisualBitmapScalingMode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(drawing, scalingMode);
                    }
                    // ?!??!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!!?!??!?!?!?!??!???!?!?????!?!?!
                    // ?!??!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!!?!??!?!?!?!??!???!?!?????!?!?!
                    // ?!??!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!!?!??!?!?!?!??!???!?!?????!?!?!

                    using(DrawingContext graphics = drawing.RenderOpen()) {
                        var scaleTransform = new ScaleTransform(scale.Scale, scale.Scale);
                        graphics.PushTransform(scaleTransform);

                        int xoff = Global.Coord.absMod((int)NW.X, 512), zoff = Global.Coord.absMod((int)NW.Y, 512);
                        int stX = Global.Coord.fairDev((int)NW.X, 512), stZ = Global.Coord.fairDev((int)NW.Y, 512);
                        for(int x = stX; x <= Global.Coord.fairDev((int)NW.X + (int)Size.X, 512); x++) {
                            for(int z = stZ; z <= Global.Coord.fairDev((int)NW.Y + (int)Size.Y, 512); z++) {
                                var tile = tileMap.GetTile(new Point2i(x, z));
                                if(tile == null) continue;
                                if(tile.img == null) continue;
                                graphics.DrawImage(tileMap.GetTile(new Point2i(x, z)).img, new Rect((x - stX) * 512 - xoff, (z - stZ) * 512 - zoff, 512, 512));
                            }
                        }
                    }
                    renderBitmap.Render(drawing);
                }

                if(rotated) return new TransformedBitmap(renderBitmap, new RotateTransform(-90));
                return renderBitmap;
            }
            catch {
                MessageBox.Show("The image couldn't generate\nThis often occurs if the image size was too big");
                return null;
            }
        }

        public void TakeAndSaveScreenshot() {
            if(resolution.type == ResolutionType.map) {
                if(resolution.X != 128 || resolution.Y != 128) {
                    MessageBox.Show("The map screenshot must be 128x128");
                    return;
                }
                TakeScreenshotAsMap(Global.App.OpenedSave.levelDatInfo.version_id);
            } else TakeScreenshotAsImage();
        }

        void TakeScreenshotAsImage() {
            var saveFileDialog = new SaveFileDialog {
                Filter = "PNG Image|*.png",
                Title = "Save screenshot",
                FileName = $"{Global.App.OpenedSave?.levelDatInfo?.name ?? "screenshot"}{resolution.X}x{resolution.Y}"
            };

            if(saveFileDialog.ShowDialog() == true) {
                var encoder = new PngBitmapEncoder();
                var screenshot = this.TakeScreenshot(BitmapScalingMode.NearestNeighbor);
                if(screenshot == null) return;
                encoder.Frames.Add(BitmapFrame.Create(screenshot));
                using(var fileStream = new FileStream(saveFileDialog.FileName, FileMode.Create)) {
                    encoder.Save(fileStream);
                }
            }
        }

        void TakeScreenshotAsMap(int version) {
            var saveFileDialog = new SaveFileDialog {
                Filter = "Dat file|*.dat",
                Title = "Save screenshot",
                FileName = $"map_"
            };

            if(saveFileDialog.ShowDialog() == true) {
                using CompoundTag_Allgemein root = new CompoundTag_Allgemein();
                var data = new CompoundTag_Allgemein();
                if(version >= 1484) root.Add("DataVersion", NumTag<int>.Get(version));
                root.Add("data", data);
                {
                    data.Add("scale", NumTag<sbyte>.Get((sbyte)Math.Log2((int)(1 / scale.Scale))));
                    data.Add("dimension", NumTag<sbyte>.Get(0));
                    data.Add("trackingPosition", NumTag<sbyte>.Get(1));
                    data.Add("unlimitedTracking", NumTag<sbyte>.Get(1));
                    data.Add("xCenter", NumTag<int>.Get((int)(Loc1.X + Loc2.X) / 2));
                    data.Add("zCenter", NumTag<int>.Get((int)(Loc1.Y + Loc2.Y) / 2));
                    if(version < 1519) {
                        data.Add("height", NumTag<short>.Get(128));
                        data.Add("width", NumTag<short>.Get(128));
                    } else {
                        data.Add("banners", ListTag.Get(TagType.Compound));
                        data.Add("frames", ListTag.Get(TagType.Compound));
                    }

                    uint[] pixels = new uint[16384];
                    var screenshot = this.TakeScreenshot(BitmapScalingMode.NearestNeighbor);
                    screenshot.CopyPixels(pixels, 512, 0);

                    var bytetag = ArrTag<byte>.Get(16384);
                    for(int i = 0; i < 16384; i++) {
                        bytetag[i] = JavaMapColors.Nearest(WPFColor.FromUInt(pixels[i]), version).id;
                    }             
                    data.Add("colors", bytetag);
                }

                using(var fs = new FileStream(saveFileDialog.FileName, FileMode.Create)) {
                    using(var zipStream = new GZipStream(fs, CompressionMode.Compress, false)) {
                        new NbtWriter(zipStream, root, "");
                    }
                }
            }
        }

        public Cursor MouseOverWhat(WorldPosition screen, Point mousePos) {
            int e = (int)Math.Round(10 + screen.zoom);
            var p = new Point(e, e).Dev(2);
            var s = new Size(e, e);

            var LocNW = Rect().TopLeft;
            var Size = Rect().Size.AsPoint();

            if(canResize) {
                if(new Rect(screen.GetLocalPos(LocNW).Sub(p), s).Contains(mousePos)) return Cursors.ScrollNW;
                if(new Rect(screen.GetLocalPos(LocNW.Add(new Point(Size.X, 0))).Sub(p), s).Contains(mousePos)) return Cursors.ScrollNE;
                if(new Rect(screen.GetLocalPos(LocNW.Add(new Point(0, Size.Y))).Sub(p), s).Contains(mousePos)) return Cursors.ScrollSW;
                if(new Rect(screen.GetLocalPos(LocNW.Add(new Point(Size.X, Size.Y))).Sub(p), s).Contains(mousePos)) return Cursors.ScrollSE;

                if(new Rect(screen.GetLocalPos(LocNW.Add(new Point(Size.X / 2, 0))).Sub(p), s).Contains(mousePos)) return Cursors.ScrollN;
                if(new Rect(screen.GetLocalPos(LocNW.Add(new Point(Size.X / 2, Size.Y))).Sub(p), s).Contains(mousePos)) return Cursors.ScrollS;
                if(new Rect(screen.GetLocalPos(LocNW.Add(new Point(0, Size.Y / 2))).Sub(p), s).Contains(mousePos)) return Cursors.ScrollW;
                if(new Rect(screen.GetLocalPos(LocNW.Add(new Point(Size.X, Size.Y / 2))).Sub(p), s).Contains(mousePos)) return Cursors.ScrollE;
            }

            if(new Rect(screen.GetLocalPos(LocNW), Size.Mult(screen.zoom).AsSize()).Contains(mousePos)) return Cursors.Cross;

            return null;
        }
    }
}
