using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Mcasaenk.UI.Canvas {
    public class WorldPosition {
        public float zoom;
        public int w, h;
        public Point2f position;

        public int GetZoomScale() {
            return (int)Math.Log2(zoom);
        }

        public Point2i GetRelativeTile(Point2f point) {
            return new Point2i(Global.Coord.absDev((position.X + point.X / zoom), 512), Global.Coord.absDev((position.Z + point.Z / zoom), 512));
        }

        public IEnumerable<Tile> GetVisibleTiles() {
            float sx = Global.Coord.absDev(position.X, 512), sz = Global.Coord.absDev(position.Z, 512), tx = Global.Coord.absMod(position.X, 512), tz = Global.Coord.absMod(position.Z, 512);

            for(int x = 0; (x * 512 - tx) * zoom < w; x++) {
                for(int z = 0; (z * 512 - tz) * zoom < h; z++) {
                    if(x + sx > -7 && x + sx < 7 && z + sz > -7 && z + sz < 7) {
                        yield return TileMap.GetTile(new Point2i(x + sx, z + sz), this);
                    }
                }
            }
        }


        public bool IsVisible(Tile tile) {
            return GetVisibleTiles().Contains(tile);
        }
    }
}
