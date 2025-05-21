using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Mcasaenk.Nbt;
using Mcasaenk.Rendering;
using Mcasaenk.Shaders;
using Mcasaenk.Shaders.Scale;
using Microsoft.Win32;
using OpenTK.Graphics.OpenGL4;

namespace Mcasaenk.UI.Canvas {

    public class ScreenshotManager : IDisposable {
        private Resolution resolution;
        private ResolutionScale scale;
        private bool rotated;
        private Point Loc1, Loc2;

        private bool locker;
        public readonly bool canResize;
        public ScreenshotManager(Resolution resolution, ResolutionScale scale, bool canResize, Point startLocation = default) {
            this.resolution = resolution;
            this.scale = scale;
            this.canResize = canResize;
            {
                Loc1 = startLocation;
                Loc2 = startLocation.Add(new Point(resolution.X, resolution.Y).Dev(scale.Scale)).Floor();
            }
            rotated = false;

            resolution.PropertyChanged += OnResolutionChange;
            scale.PropertyChanged += OnScaleChange;
        }
        ~ScreenshotManager() { Dispose(); }
        bool disposed = false;
        public void Dispose() {
            if(!disposed) {
                disposed = true;

                resolution.PropertyChanged -= OnResolutionChange;
                scale.PropertyChanged -= OnScaleChange;
            }
        }

        public ResolutionType ResolutionType() => resolution.type;

        private void OnResolutionChange(object s, System.ComponentModel.PropertyChangedEventArgs e) {
            if(locker) return;
            if(e.PropertyName == nameof(resolution.X) || e.PropertyName == nameof(resolution.Y)) {
                var size = new Point(resolution.X, resolution.Y).Mult(scale.Scale);
                Loc1 = AsRect().TopLeft.Add(size.Dev(-2));
                Loc2 = Loc1.Add(size);
            }
        }
        private void OnScaleChange(object s, System.ComponentModel.PropertyChangedEventArgs e) {
            if(locker) return;
            if(e.PropertyName == nameof(scale.Scale)) {
                Rescale();
            }
        }
        private void Rescale() {
            locker = true;
            var mid = AsRect().Mid();

            int x = (int)(resolution.X / scale.Scale), y = (int)(resolution.Y / scale.Scale);
            if(rotated) (x, y) = (y, x);

            Loc1 = mid.Sub(new Point(x, y).Dev(2)).Floor();
            Loc2 = mid.Add(new Point(x, y).Dev(2)).Floor();
            locker = false;
        }


        public Rect AsRect() => new Rect(Loc1, Loc2);
        public WorldPosition AsScreen() {
            Rect r = AsRect();
            return new WorldPosition(r.TopLeft, r.Width, r.Height, scale.Scale);
        }
        public Point2i Resolution() => new Point2i(resolution.X, resolution.Y);

        public void Rotate() {
            rotated = !rotated;

            var mid = AsRect().Mid();
            var r = mid.Sub(Loc1);

            Loc1 = mid.Sub(new Point(r.Y, r.X));
            Loc2 = mid.Add(new Point(r.Y, r.X));
        }
        public bool IsRotated() => rotated;
        public void ResizeCorner(Point p) {
            locker = true;
            Loc1 = p;
            resolution.X = (int)(Math.Abs(Loc1.X - Loc2.X) * scale.Scale);
            resolution.Y = (int)(Math.Abs(Loc1.Y - Loc2.Y) * scale.Scale);
            locker = false;
        }
        public void ResizeAxis(int a, bool x) {
            locker = true;
            if (x) {
                Loc1.X = a;
                resolution.X = (int)(Math.Abs(Loc1.X - Loc2.X) * scale.Scale);
            } else {
                Loc1.Y = a;
                resolution.Y = (int)(Math.Abs(Loc1.Y - Loc2.Y) * scale.Scale);
            }
            locker = false;
        }
        public void Move(Point byHow) {
            Loc1 = Loc1.Add(byHow);
            Loc2 = Loc2.Add(byHow);
        }
        public void Rebase(bool north, bool west) {
            Point new1 = new Point(0, 0), new2 = new Point();

            if (north) {
                new1.Y = Math.Min(Loc1.Y, Loc2.Y);
                new2.Y = Math.Max(Loc1.Y, Loc2.Y);
            } else {
                new1.Y = Math.Max(Loc1.Y, Loc2.Y);
                new2.Y = Math.Min(Loc1.Y, Loc2.Y);
            }

            if (west) {
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
        public Cursor MouseOverWhat(WorldPosition screen, Point mousePos) {
            double e = (10 + screen.zoom);
            var p = new Point(e, e).Dev(2).Add(new Point(0, 0.5 * screen.zoom));
            var s = new Size(e, e);

            var LocNW = AsRect().TopLeft;
            var Size = AsRect().Size.AsPoint();

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


        public enum ConditionalState : uint { 
            ok                = 0xFF00FF00, 
            shadesnotfinished = 0xFFA5FF00, 
            unloadedchunks   = 0xFFFFA500,
            invalid           = 0xFFFF0000,
        }
        public ConditionalState GetState(GenDataTileMap gentilemap) {
            if(resolution.X > 16384 || resolution.Y > 16384) return ConditionalState.invalid;

            var screen = this.AsScreen();

            foreach(var rch in gentilemap.GetVisibleChunkPositions(screen)) { 
                if(gentilemap.GetTile(rch.reg, out var tile) == false) return ConditionalState.unloadedchunks;
                if(tile.IsChunkScreenshotable(rch.chunk.X, rch.chunk.Z) == false) return ConditionalState.unloadedchunks;
            }
            foreach(var reg in gentilemap.GetVisibleTilesPositions(screen)) {
                if(!gentilemap.GetTile(reg, out var tile)) return ConditionalState.unloadedchunks;

                if(gentilemap.GetShadeTile(reg) is not { IsActive: false })
                    return ConditionalState.shadesnotfinished;
            }
            return ConditionalState.ok;
        }


        //private BitmapSource TakeScreenshot(BitmapScalingMode scalingMode) {
        //    try {
        //        var Size = Rect().Size.AsPoint();
        //        var NW = Rect().TopLeft;

        //        var (rx, ry) = (resolution.X, resolution.Y);
        //        if (rotated) (ry, rx) = (resolution.X, resolution.Y);
        //        var renderBitmap = new RenderTargetBitmap(rx, ry, 96, 96, PixelFormats.Pbgra32);
        //        if (tileMap != null) {
        //            var drawing = new DrawingVisual();

        //            // ?!??!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!!?!??!?!?!?!??!???!?!?????!?!?!
        //            // ?!??!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!!?!??!?!?!?!??!???!?!?????!?!?!
        //            // ?!??!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!!?!??!?!?!?!??!???!?!?????!?!?!
        //            {
        //                /* totally not bugged as fuck
        //                        RenderOptions.SetBitmapScalingMode(drawing, BitmapScalingMode.NearestNeighbor);
        //                        RenderOptions.SetEdgeMode(drawing, EdgeMode.Aliased);
        //                 */

        //                drawing.GetType().GetProperty("VisualEdgeMode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(drawing, EdgeMode.Aliased);
        //                drawing.GetType().GetProperty("VisualBitmapScalingMode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(drawing, scalingMode);
        //            }
        //            // ?!??!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!!?!??!?!?!?!??!???!?!?????!?!?!
        //            // ?!??!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!!?!??!?!?!?!??!???!?!?????!?!?!
        //            // ?!??!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!?!!?!??!?!?!?!??!???!?!?????!?!?!

        //            using (DrawingContext graphics = drawing.RenderOpen()) {
        //                var scaleTransform = new ScaleTransform(scale.Scale, scale.Scale);
        //                graphics.PushTransform(scaleTransform);

        //                int xoff = Global.Coord.absMod((int)NW.X, 512), zoff = Global.Coord.absMod((int)NW.Y, 512);
        //                int stX = Global.Coord.fairDev((int)NW.X, 512), stZ = Global.Coord.fairDev((int)NW.Y, 512);
        //                for (int x = stX; x <= Global.Coord.fairDev((int)NW.X + (int)Size.X, 512); x++) {
        //                    for (int z = stZ; z <= Global.Coord.fairDev((int)NW.Y + (int)Size.Y, 512); z++) {
        //                        var tile = tileMap.GetTile(new Point2i(x, z));
        //                        if (tile == null) continue;
        //                        //if(tile.img == null) continue;
        //                        //graphics.DrawImage(tileMap.GetTile(new Point2i(x, z)).img, new Rect((x - stX) * 512 - xoff, (z - stZ) * 512 - zoff, 512, 512));
        //                    }
        //                }
        //            }
        //            renderBitmap.Render(drawing);
        //        }

        //        if (rotated) return new TransformedBitmap(renderBitmap, new RotateTransform(-90));
        //        return renderBitmap;
        //    } catch {
        //        MessageBox.Show("The image couldn't generate\nThis often occurs if the image size was too big");
        //        return null;
        //    }
        //}

        //public void TakeAndSaveScreenshot() {
        //    if (resolution.type == ResolutionType.map) {
        //        if (resolution.X != 128 || resolution.Y != 128) {
        //            MessageBox.Show("The map screenshot must be 128x128");
        //            return;
        //        }
        //        TakeScreenshotAsMap(Global.App.OpenedSave.levelDatInfo.version_id);
        //    } else TakeScreenshotAsImage();
        //}

        //void TakeScreenshotAsImage() {
        //    var saveFileDialog = new SaveFileDialog {
        //        Filter = "PNG Image|*.png",
        //        Title = "Save screenshot",
        //        FileName = $"{Global.App.OpenedSave?.levelDatInfo?.name ?? "screenshot"}{resolution.X}x{resolution.Y}"
        //    };

        //    if (saveFileDialog.ShowDialog() == true) {
        //        var encoder = new PngBitmapEncoder();
        //        var screenshot = this.TakeScreenshot(BitmapScalingMode.NearestNeighbor);
        //        if (screenshot == null) return;
        //        encoder.Frames.Add(BitmapFrame.Create(screenshot));
        //        using (var fileStream = new FileStream(saveFileDialog.FileName, FileMode.Create)) {
        //            encoder.Save(fileStream);
        //        }
        //    }
        //}

        //void TakeScreenshotAsMap(int version) {
        //    var saveFileDialog = new SaveFileDialog {
        //        Filter = "Dat file|*.dat",
        //        Title = "Save screenshot",
        //        FileName = $"map_"
        //    };

        //    if (saveFileDialog.ShowDialog() == true) {
        //        using CompoundTag_Allgemein root = new CompoundTag_Allgemein();
        //        var data = new CompoundTag_Allgemein();
        //        if (version >= 1484) root.Add("DataVersion", NumTag<int>.Get(version));
        //        root.Add("data", data);
        //        {
        //            data.Add("scale", NumTag<sbyte>.Get((sbyte)Math.Log2((int)(1 / scale.Scale))));
        //            data.Add("dimension", NumTag<sbyte>.Get(0));
        //            data.Add("trackingPosition", NumTag<sbyte>.Get(1));
        //            data.Add("unlimitedTracking", NumTag<sbyte>.Get(1));
        //            data.Add("xCenter", NumTag<int>.Get((int)(Loc1.X + Loc2.X) / 2));
        //            data.Add("zCenter", NumTag<int>.Get((int)(Loc1.Y + Loc2.Y) / 2));
        //            if (version < 1519) {
        //                data.Add("height", NumTag<short>.Get(128));
        //                data.Add("width", NumTag<short>.Get(128));
        //            } else {
        //                data.Add("banners", ListTag.Get(TagType.Compound));
        //                data.Add("frames", ListTag.Get(TagType.Compound));
        //            }

        //            uint[] pixels = new uint[16384];
        //            var screenshot = this.TakeScreenshot(BitmapScalingMode.NearestNeighbor);
        //            screenshot.CopyPixels(pixels, 512, 0);

        //            var bytetag = ArrTag<byte>.Get(16384);
        //            for (int i = 0; i < 16384; i++) {
        //                bytetag[i] = JavaMapColors.Nearest(WPFColor.FromUInt(pixels[i]), version).id;
        //            }
        //            data.Add("colors", bytetag);
        //        }

        //        using (var fs = new FileStream(saveFileDialog.FileName, FileMode.Create)) {
        //            using (var zipStream = new GZipStream(fs, CompressionMode.Compress, false)) {
        //                new NbtWriter(zipStream, root, "");
        //            }
        //        }
        //    }
        //}
    }

    public interface ScreenshotTaker {
        BitmapSource TakeScreenshotAsImage();
        CompoundTag_Allgemein TakeScreenshotAsMap(int version);
    }

    public class OpenGLScreenshotTaker : ScreenshotTaker, IDisposable {
        private readonly GenDataTileMap gentilemap;
        private readonly ShaderPipeline renderer;
        private readonly WorldPosition frame;
        private readonly bool rotate;

        private readonly ShaderTexture2D sceneimage;
        public OpenGLScreenshotTaker(GenDataTileMap gentilemap, ShaderPipeline renderer, WorldPosition frame, bool rotate) { 
            this.gentilemap = gentilemap;
            this.renderer = renderer;
            this.frame = frame;
            this.rotate = rotate;

            sceneimage = ShaderTexture2D.CreateRGBA8_Single((int)frame.Width, (int)frame.Height);
        }
        public void Dispose() {
            sceneimage.Dispose();
        }

        public BitmapSource TakeScreenshotAsImage() {
            int w = frame.ScreenWidth, h = frame.ScreenHeight;
            if(rotate) (w, h) = (h, w);
            return BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, Render(), w * 4);
        }

        public CompoundTag_Allgemein TakeScreenshotAsMap(int version) {
            return NBTBlueprints.CreateMapScreenshot(MemoryMarshal.Cast<byte, uint>(Render()), frame, version);
        }


        private byte[] Render() {
            renderer.Render(frame, gentilemap, sceneimage.textureHandle);

            byte[] data = sceneimage.ReadData();
            if(frame.zoom > 1) data = ScaleUpRaw32bit(data, (int)frame.Width, (int)frame.Height, (int)frame.zoom);
            FlipVertAndConvertRgbaToBgra(data, frame.ScreenWidth, frame.ScreenHeight);
            if(rotate) data = RotateMinus90(data, frame.ScreenWidth, frame.ScreenHeight);
            return data;
        }


        static void FlipVertAndConvertRgbaToBgra(byte[] rgba, int width, int height) {
            int stride = width * 4;

            // Swap rows from top and bottom
            for(int y = 0; y < height / 2; y++) {
                int topRowStart = y * stride;
                int bottomRowStart = (height - 1 - y) * stride;

                for(int x = 0; x < width; x++) {
                    int topIndex = topRowStart + x * 4;
                    int bottomIndex = bottomRowStart + x * 4;

                    // Swap pixels between top and bottom row with RGBA->BGRA conversion

                    // Top pixel RGBA
                    byte rTop = rgba[topIndex];
                    byte gTop = rgba[topIndex + 1];
                    byte bTop = rgba[topIndex + 2];
                    byte aTop = rgba[topIndex + 3];

                    // Bottom pixel RGBA
                    byte rBottom = rgba[bottomIndex];
                    byte gBottom = rgba[bottomIndex + 1];
                    byte bBottom = rgba[bottomIndex + 2];
                    byte aBottom = rgba[bottomIndex + 3];

                    // Write bottom pixel (converted BGRA) to top position
                    rgba[topIndex] = bBottom;       // B
                    rgba[topIndex + 1] = gBottom;   // G
                    rgba[topIndex + 2] = rBottom;   // R
                    rgba[topIndex + 3] = aBottom;   // A

                    // Write top pixel (converted BGRA) to bottom position
                    rgba[bottomIndex] = bTop;
                    rgba[bottomIndex + 1] = gTop;
                    rgba[bottomIndex + 2] = rTop;
                    rgba[bottomIndex + 3] = aTop;
                }
            }

            // If height is odd, flip/convert the middle row in place
            if(height % 2 == 1) {
                int middleRowStart = (height / 2) * stride;
                for(int x = 0; x < width; x++) {
                    int idx = middleRowStart + x * 4;
                    byte r = rgba[idx];
                    byte g = rgba[idx + 1];
                    byte b = rgba[idx + 2];
                    byte a = rgba[idx + 3];

                    rgba[idx] = b;
                    rgba[idx + 1] = g;
                    rgba[idx + 2] = r;
                    rgba[idx + 3] = a;
                }
            }
        }

        static byte[] ScaleUpRaw32bit(byte[] srcPixels, int width, int height, int scale) {
            int bytesPerPixel = 4;
            int srcStride = width * bytesPerPixel;
            int newWidth = width * scale;
            int newHeight = height * scale;
            int destStride = newWidth * bytesPerPixel;

            byte[] destPixels = new byte[newWidth * newHeight * bytesPerPixel];

            for(int y = 0; y < height; y++) {
                for(int x = 0; x < width; x++) {
                    int srcIndex = y * srcStride + x * bytesPerPixel;

                    // Copy this pixel into a scale x scale block
                    for(int dy = 0; dy < scale; dy++) {
                        for(int dx = 0; dx < scale; dx++) {
                            int destX = x * scale + dx;
                            int destY = y * scale + dy;
                            int destIndex = destY * destStride + destX * bytesPerPixel;

                            System.Buffer.BlockCopy(srcPixels, srcIndex, destPixels, destIndex, bytesPerPixel);
                        }
                    }
                }
            }

            return destPixels;
        }

        static byte[] RotateMinus90(byte[] input, int width, int height) {
            int bytesPerPixel = 4;
            byte[] output = new byte[input.Length];

            for(int y = 0; y < height; y++) {
                for(int x = 0; x < width; x++) {
                    int srcIndex = (y * width + x) * bytesPerPixel;

                    int dstX = y;
                    int dstY = width - 1 - x;
                    int dstIndex = (dstY * height + dstX) * bytesPerPixel;

                    for(int b = 0; b < bytesPerPixel; b++) {
                        output[dstIndex + b] = input[srcIndex + b];
                    }
                }
            }

            return output;
        }
    }
}
