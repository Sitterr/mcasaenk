namespace Mcasaenk {
    public struct Point2i {
        public int X { get; set; }
        public int Z { get; set; }

        public Point2i(int x, int z) {
            this.X = x; this.Z = z;
        }
        public Point2i(System.Windows.Point point) : this((int)point.X, (int)point.Y) { }
        public Point2i(double x, double z) : this((int)x, (int)z) { }

        public static Point2i operator +(Point2i a, Point2i b) => new Point2i(a.X + b.X, a.Z + b.Z);
        public static Point2i operator -(Point2i a, Point2i b) => new Point2i(a.X - b.X, a.Z - b.Z);
        public static Point2i operator *(Point2i a, Point2i b) => new Point2i(a.X * b.X, a.Z * b.Z);
        public static Point2i operator /(Point2i a, Point2i b) => new Point2i((int)Math.Floor((double)a.X / b.X), (int)Math.Floor((double)a.Z / b.Z));
        public static Point2i operator +(Point2i a, int b) => new Point2i(a.X + b, a.Z + b);
        public static Point2i operator *(Point2i a, float f) => new Point2i((int)(a.X * f), (int)(a.Z * f));
        public static Point2i operator /(Point2i a, float f) => new Point2i((int)(a.X / f), (int)(a.Z / f));
        public static Point2i operator %(Point2i a, int f) => new Point2i(Global.Coord.absMod(a.X, f), Global.Coord.absMod(a.Z, f));
        public static bool operator ==(Point2i a, Point2i b) => a.X == b.X && a.Z == b.Z;
        public static bool operator !=(Point2i a, Point2i b) => !(a == b);

        public int ToRegionInt() => Z * 512 + X;

        public Point2i abs() => new Point2i(Math.Abs(this.X), Math.Abs(this.Z));
        public override string ToString() {
            return $"{X},{Z}";
        }

        public static Point2i NULL = new Point2i(int.MinValue, int.MinValue);
    }
}
