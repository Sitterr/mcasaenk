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
using System.Collections.Frozen;

namespace Mcasaenk
{
    public class TileMap {
        public readonly Dimension dimension;
        private readonly ConcurrentDictionary<long, Tile> tiles;
        private readonly ConcurrentDictionary<long, TileShadeFrames> shadeFrames;
        private readonly FrozenSet<long> possibleTiles;

        public GenerateTilePool generateTilePool;
        public TilePool quickTilePool;

        public TileMap(Dimension dimension, HashSet<Point2i> existingRegions) {
            this.dimension = dimension;
            this.possibleTiles = existingRegions.Select(a => a.AsLong()).ToFrozenSet();
            tiles = new ConcurrentDictionary<long, Tile>();
            shadeFrames = new ConcurrentDictionary<long, TileShadeFrames>();
        }

        public void SetSettings() {
            generateTilePool = new GenerateTilePool();
            quickTilePool = new TilePool(4);

            ColorMapping.Init();
            //GC.Collect(2, GCCollectionMode.Forced);
        }

        public Tile GetTile(Point2i point) {
            long pointL = point.AsLong();
            if(!possibleTiles.Contains(pointL)) return null;
            Tile tile;
            if(tiles.TryGetValue(pointL, out tile) == false) {
                tile = new Tile(this, point);
                tiles.TryAdd(pointL, tile);
            }
            return tile;
        }

        public TileShadeFrames GetTileShadeFrame(Point2i point) {
            long pointL = point.AsLong();
            TileShadeFrames fr;
            if(!shadeFrames.TryGetValue(pointL, out fr)) {
                fr = new TileShadeFrames(this, point);
                shadeFrames.TryAdd(pointL, fr);
            }
            return fr;
        }

        public int ShadeTiles() => tiles.Values.Where(t => t.contgen.IsActive).Count();

        public int ShadeFrames() => shadeFrames.Values.Select(t => t.frames.Count).Sum();
    }

    public class Tile {
       

        public TileImage image;
        public TileShade shade;
        public TileGenData contgen;

        public readonly Point2i pos;
        private readonly TileMap map;
        public Tile(TileMap tileMap, Point2i position) {
            this.map = tileMap;
            this.pos = position;
            image = new TileImage(this);
            shade = new TileShade(this);

            contgen = new TileGenData(shade);
        }

        public void QueueGenerate(WorldPosition observer) {
            map.generateTilePool.Queue(this, observer);
        }
        public void QueueGenUpdate() {
            map.quickTilePool.Queue(this, () => {
                //Task.Delay(100).Wait();
                this.image.Redraw();
            }, () => true);
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
