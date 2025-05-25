using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Mcasaenk.UI.Canvas {
    public struct WorldPosition {
        private double _zoom;
        private Rect coord;

        public WorldPosition(Point start, double width, double height, double zoom) {
            this.coord = new Rect(start.X, start.Y, width, height);
            _zoom = zoom;
        }

        public Point Start {
            get {
                return coord.Location;
            }
            set { 
                coord.Location = new Point(Double.Round(value.X, 5), Double.Round(value.Y, 5));
            }
        }

        public Point Mid {
            get {
                return new Point(coord.X + coord.Width / 2, coord.Y + coord.Height / 2);
            }
            set { 
                coord.X = value.X - coord.Width / 2;
                coord.Y = value.Y - coord.Height / 2;
            }
        }

        public int ScreenWidth {
            get {
                return (int)Math.Ceiling(coord.Width * zoom);
            }
            set {
                coord.Width = value / zoom;
            }
        }
        public int ScreenHeight {
            get {
                return (int)Math.Ceiling(coord.Height * zoom);
            }
            set {
                coord.Height = value / zoom;
            }
        }
        public double Width {
            get { return coord.Width; }
        }
        public double Height {
            get { return coord.Height; }
        }
        public double zoom {
            get {
                return _zoom;
            }
            set {
                int screenw = ScreenWidth, screenh = ScreenHeight;
                _zoom = value;
                ScreenWidth = screenw;
                ScreenHeight = screenh;
            }
        }

        public int ZoomScale {
            get {
                return (int)Math.Log2(zoom);
            }

            set {
                zoom = Math.Pow(2, value);
            }
        }

        public double InSimZoom  { get => zoom > 1 ? 1 : zoom; }
        public double OutSimzoom { get => zoom < 1 ? 1 : zoom; }

        public Point GetGlobalPos(Point rel) {
            return new Point(coord.X + rel.X / zoom, coord.Y + rel.Y / zoom);
        }
        public Point GetLocalPos(Point gl) {
            return (gl.Sub(this.Start)).Mult(this.zoom);
        }

        public bool InterSects(WorldPosition p1) { 
            return coord.IntersectsWith(p1.coord);
        }

        public WorldPosition Extend(double r) {
            return new WorldPosition(this.Start.Add(new Point(-r, -r)), Width + 2*r, Height + 2*r, zoom);
        }

        public static bool operator ==(WorldPosition a, WorldPosition b) => a.Start == b.Start && a.Width == b.Width && a.Height == b.Height;
        public static bool operator !=(WorldPosition a, WorldPosition b) => !(a == b);

        //public IEnumerable<Point2i> GetVisibleTilePositions() {
        //    double sx = Global.Coord.fairDev((int)Math.Floor(coord.X), 512), sz = Global.Coord.fairDev((int)Math.Floor(coord.Y), 512), tx = Global.Coord.absMod(coord.X, 512), tz = Global.Coord.absMod(coord.Y, 512);

        //    for(int x = 0; x * 512 - tx <= coord.Width + 1; x++) {
        //        for(int z = 0; z * 512 - tz <= coord.Height + 1; z++) {
        //            yield return new Point2i(x + sx, z + sz);
        //        }
        //    }
        //}

        //public bool IsVisible(Tile tile) {
        //    Rect tileArea = new Rect(tile.pos.X * 512, tile.pos.Z * 512, 512, 512);
        //    return coord.IntersectsWith(tileArea);
        //}

    }
}
