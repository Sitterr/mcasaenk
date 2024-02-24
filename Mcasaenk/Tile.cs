using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Runtime.Intrinsics.Arm;
using System.Windows.Media.Media3D;
using System.Diagnostics;
using Mcasaenk.UI.Canvas;
using System.IO;
using System.Collections.Concurrent;
using Mcasaenk.Rendering;
using Mcasaenk.Shade3d;
using static Mcasaenk.Rendering.GenerateTilePool;

namespace Mcasaenk
{
    public class TileMap {
        public readonly Dimension dimension;
        private readonly ConcurrentDictionary<Point2i, Tile> tiles;
        private readonly ConcurrentDictionary<Point2i, TileShadeFrames> shadeFrames;
        private readonly HashSet<Point2i> possibleTiles;

        public GenerateTilePool generateTilePool;
        public TilePool quickTilePool;

        public TileMap(Dimension dimension, HashSet<Point2i> existingRegions) {
            this.dimension = dimension;
            this.possibleTiles = existingRegions;
            tiles = new ConcurrentDictionary<Point2i, Tile>();
            shadeFrames = new ConcurrentDictionary<Point2i, TileShadeFrames>();
        }

        public void SetSettings() {
            generateTilePool = new GenerateTilePool();
            quickTilePool = new TilePool(4);

            ColorMapping.Init();
        }

        public Tile GetTile(Point2i point) {
            if(!possibleTiles.Contains(point)) return null;
            Tile tile;
            if(tiles.TryGetValue(point, out tile) == false) {
                tile = new Tile(this, point);
                tiles.TryAdd(point, tile);
            }
            return tile;
        }

        public TileShadeFrames GetTileShadeFrame(Point2i point) {
            TileShadeFrames fr;
            if(!shadeFrames.TryGetValue(point, out fr)) {
                fr = new TileShadeFrames(this, point);
                shadeFrames.TryAdd(point, fr);
            }
            return fr;
        }

        public int ShadeTiles() => tiles.Values.Where(t => t.shade.active).Count();

        public int ShadeFrames() => shadeFrames.Values.Select(t => t.frames.Count).Sum();
    }

    public class Tile {
       

        public TileImage image;
        public TileShade shade;

        public readonly Point2i pos;
        private readonly TileMap map;
        public Tile(TileMap tileMap, Point2i position) {
            this.map = tileMap;
            this.pos = position;
            image = new TileImage(this);
            shade = new TileShade(this);
        }

        public void QueueGenerate(WorldPosition observer) {
            map.generateTilePool.Queue(this, observer);
        }
        public void QueueShadeUpdate() {
            map.quickTilePool.Queue(this, () => {
                Task.Delay(1000).Wait();
                this.image.Redraw();
            }, this.shade.ShouldRedraw);
        }
        public bool IsLoading() {
            bool res = false;
            res |= map.generateTilePool.IsLoading(this);
            res |= map.quickTilePool.IsLoading(this);
            return res;
        }

        public ImageSource GetImage() {
            return image.GetImage();
        }

        public TileMap GetOrigin() { 
            return map;
        }
    }
}
