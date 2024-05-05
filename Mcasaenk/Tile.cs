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
using System.Collections.Frozen;

namespace Mcasaenk
{
    public class TileMap {
        public readonly Dimension dimension;
        private readonly ConcurrentDictionary<long, Tile> tiles;
        private readonly ConcurrentDictionary<long, TileShadeFrames> shadeFrames;
        private readonly FrozenSet<long> possibleTiles;

        public GenerateTilePool generateTilePool;
        public TilePool drawTilePool;

        public TileMap(Dimension dimension, HashSet<Point2i> existingRegions) {
            this.dimension = dimension;
            this.possibleTiles = existingRegions.Select(a => a.AsLong()).ToFrozenSet();
            tiles = new ConcurrentDictionary<long, Tile>();
            shadeFrames = new ConcurrentDictionary<long, TileShadeFrames>();
        }

        public void SetSettings() {
            generateTilePool = new GenerateTilePool();
            drawTilePool = new TilePool(8);

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

        public int ShadeTiles() => tiles.Values.Where(t => t.shade.IsActive).Count();

        public int ShadeFrames() => shadeFrames.Values.Select(t => t.frames.Count).Sum();
    }

    public class Tile {
        public TileShade shade;
        public List<GenDataEditor> editors;

        private bool shouldRedraw;
        public bool ShouldRedraw { 
            get {
                bool val = shouldRedraw;
                foreach(var editor in editors) val = val || editor.ShouldRedraw;
                val = val && map.generateTilePool.HasLoaded(this);
                return val;
            } 
            set {
                shouldRedraw = shouldRedraw || value;
            } 
        }
        private void ResetShouldRedraw() {
            shouldRedraw = false;
            foreach(var editor in editors) editor.ShouldRedraw = false;
        }


        public readonly Point2i pos;
        private readonly TileMap map;
        public Tile(TileMap tileMap, Point2i position) {
            this.map = tileMap;
            this.pos = position;
            //image = new TileImage(this);
            shade = new TileShade(this);

            editors = [shade];
        }

        public void QueueGenerate(WorldPosition observer) {
            map.generateTilePool.Queue(this, observer);
        }
        public void QueueDraw() {
            map.drawTilePool.Queue(this, () => {
                //Task.Delay(100).Wait();
                this.Redraw();
            }, () => !this.IsLoading());
        }
        public bool IsLoading() => map.generateTilePool.IsLoading(this);
        public bool IsRedrawing() => map.drawTilePool.IsLoading(this);

        public TileMap GetOrigin() { 
            return map;
        }




        private GenData _genData;
        public GenData genData {
            get {
                return _genData;
            }
            set {
                _genData = value;
                if(Settings.LAND_BLEND > 1) {
                    for(int i = -1; i <= 1; i++) { // biome blend
                        for(int j = -1; j <= 1; j++) {
                            var tile = map.GetTile(pos + new Point2i(i, j));
                            if(tile == null) continue;
                            tile.ShouldRedraw = true;
                        }
                    }
                } else this.ShouldRedraw = true;
            }
        }
        private WriteableBitmap _img;
        public WriteableBitmap img {
            get {
                return _img;
            }
            set {
                _img = value;
            }
        }





        public unsafe void Redraw() {
            var img = this.img == null ? new WriteableBitmap(512, 512, 96, 96, PixelFormats.Bgra32, null) : this.img.Clone();
            img.Lock();

            uint* pixels = (uint*)img.BackBuffer;
            GenData[,] neighbours = null;
            if(Settings.LAND_BLEND > 1 || Settings.WATER_BLEND > 1) {
                neighbours = new GenData[3, 3];
                for(int i = -1; i <= 1; i++) { // biome blend
                    for(int j = -1; j <= 1; j++) {
                        var tile = map.GetTile(pos + new Point2i(i, j));
                        if(tile == null) continue;
                        neighbours[i + 1, j + 1] = tile.genData;
                    }
                }
            }
            TileDraw.FillPixels(new Span<uint>(pixels, 512 * 512), this.genData, neighbours);

            img.Unlock();
            img.Freeze();

            this.img = img;
            ResetShouldRedraw();
        }
    }
}
