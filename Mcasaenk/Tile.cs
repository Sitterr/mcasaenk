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
using System.Buffers;
using static Mcasaenk.Rendering.Tint;

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
            ColorGausBlur.pool = ArrayPool<ColorGausBlur.C>.Create((512 * 3) * (512 * 3), Global.Settings.DRAWMAXCONCURRENCY);
            PrecGausBlur.pool = ArrayPool<ushort>.Create((512 * 3) * (512 * 3) * PrecGausBlur.MB, Global.Settings.DRAWMAXCONCURRENCY);

            generateTilePool = new GenerateTilePool();
            drawTilePool = new TilePool(Global.Settings.DRAWMAXCONCURRENCY);
            
            GC.Collect(2, GCCollectionMode.Forced);
        }

        public void RedrawAll() {
            foreach(var tile in tiles) {
                drawTilePool.RegisterRedo(tile.Value);
            }
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


        public void RegisterRedraw() { 
            map.drawTilePool.RegisterRedo(this);
        }

        public readonly Point2i pos;
        private readonly TileMap map;
        public Tile(TileMap tileMap, Point2i position) {
            this.map = tileMap;
            this.pos = position;
            shade = new TileShade(this);

            map.generateTilePool.RegisterRedo(this);
            editors = [shade];
        }

        public void QueueGenerate(WorldPosition observer) {
            map.generateTilePool.Queue(this, observer);
        }
        public void QueueDraw() {
            map.drawTilePool.Queue(this, () => {
                this.Redraw();
            }, () => true);
        }

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
                if(Global.App.Colormap.GetTints().Any(t => t.GetBlendMode() == Blending.biomeonly || t.GetBlendMode() == Blending.full)) {
                    for(int i = -1; i <= 1; i++) { // biome blend
                        for(int j = -1; j <= 1; j++) {
                            var tile = map.GetTile(pos + new Point2i(i, j));
                            if(tile == null) continue;
                            if(tile.genData != null) tile.RegisterRedraw();
                        }
                    }
                } else this.RegisterRedraw();
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
            if(this.genData == null) return;
            var img = new WriteableBitmap(512, 512, 96, 96, PixelFormats.Bgra32, null);
            img.Lock();

            uint* pixels = (uint*)img.BackBuffer;
            GenData[,] neighbours = null;
            if(Global.App.Colormap.GetTints().Any(t => t.GetBlendMode() == Blending.biomeonly || t.GetBlendMode() == Blending.full)) {
                neighbours = new GenData[3, 3];
                for(int i = -1; i <= 1; i++) { // biome blend
                    for(int j = -1; j <= 1; j++) {
                        var tile = map.GetTile(pos + new Point2i(i, j));
                        if(tile == null) continue;
                        neighbours[i + 1, j + 1] = tile.genData;
                    }
                }
            }
            TileDraw.FillPixels(pixels, Global.App.Colormap, this.genData, neighbours);

            img.Unlock();
            img.Freeze();

            this.img = img;
        }
    }
}
