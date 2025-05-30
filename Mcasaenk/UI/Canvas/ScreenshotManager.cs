using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Mcasaenk.Nbt;
using Mcasaenk.Rendering;

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
        public void Dispose() {
            resolution.PropertyChanged -= OnResolutionChange;
            scale.PropertyChanged -= OnScaleChange;
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
            if(x) {
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
        public void Teleport(Point where) {
            Loc1 = where;
            Loc2 = where.Add(new Point(resolution.X, resolution.Y).Mult(scale.Scale)).Floor();
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
        public Cursor MouseOverWhat(WorldPosition screen, Point mousePos, bool opengl = false) {
            double e = (10 + screen.zoom);
            var p = new Point(e, e).Dev(2);
            if(opengl) p = p.Add(new Point(0, 0.5 * screen.zoom));
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
            ok = 0xFF00FF00,
            shadesnotfinished = 0xFFA5FF00,
            unloadedchunks = 0xFFFFA500,
            invalid = 0xFFFF0000,
        }
        public ConditionalState GetState(GenDataTileMap gentilemap) {
            if(resolution.X > 16384 || resolution.Y > 16384) return ConditionalState.invalid;
            if(gentilemap == null) return ConditionalState.invalid;

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

    }

    public interface ScreenshotTaker {
        BitmapSource TakeScreenshotAsImage();
        CompoundTag_Allgemein TakeScreenshotAsMap(Dimension dim, int version, ColorApproximationAlgorithm coloralgo);
    }
}
