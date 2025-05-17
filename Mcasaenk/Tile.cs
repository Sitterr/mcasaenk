//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System.Windows;
//using System.Security.Cryptography;
//using System.Runtime.CompilerServices;
//using System.Threading;
//using System.Runtime.Intrinsics.Arm;
//using System.Windows.Media.Media3D;
//using System.Diagnostics;
//using Mcasaenk.UI.Canvas;
//using System.IO;
//using System.Collections.Concurrent;
//using Mcasaenk.Rendering;
//using Mcasaenk.Shade3d;
//using System.Collections.Frozen;
//using System.Buffers;
//using static Mcasaenk.Colormaping.Tint;
//using static System.Net.WebRequestMethods;
//using Mcasaenk.Shaders;
//using OpenTK.Graphics.OpenGL4;
//using System.Windows.Media.Imaging;
//using Microsoft.Win32.SafeHandles;

//namespace Mcasaenk
//{
//    public class TileMap : IDisposable {
//        public readonly Dimension dimension;
//        private readonly ConcurrentDictionary<long, Tile> tiles;
//        private readonly ConcurrentDictionary<long, TileShadeFrames> shadeFrames;
//        private readonly FrozenSet<long> possibleTiles;

//        public GenerateTilePool generateTilePool;
//        public TilePool drawTilePool;

//        public TileMap(Dimension dimension, HashSet<Point2i> existingRegions) {
//            this.dimension = dimension;
//            this.possibleTiles = existingRegions.Select(a => a.AsLong()).ToFrozenSet();
//            tiles = new ConcurrentDictionary<long, Tile>();
//            shadeFrames = new ConcurrentDictionary<long, TileShadeFrames>();
//        }

//        private bool disposed;
//        public void Dispose() {
//            if(disposed) return;

//            foreach(var tile in tiles) {
//                tile.Value.genData?.Dispose();
//                if(tile.Value.texture != -1) GL.DeleteTexture(tile.Value.texture);
//            }

//            disposed = true;
//        }

//        public void SetSettings() {
//            //ColorGausBlur.pool = ArrayPool<ColorGausBlur.C>.Create((512 * 3) * (512 * 3), Global.Settings.DRAWMAXCONCURRENCY);
//            //PrecGausBlur.pool = ArrayPool<ushort>.Create((512 * 3) * (512 * 3) * PrecGausBlur.MB, Global.Settings.DRAWMAXCONCURRENCY);

//            generateTilePool = new GenerateTilePool();
//            drawTilePool = new TilePool(Global.Settings.DRAWMAXCONCURRENCY);
            
//            GC.Collect(2, GCCollectionMode.Forced);
//        }

//        public volatile bool _update;
//        public void RedrawAll() {
//            foreach(var tile in tiles) {
//                drawTilePool.RegisterRedo(tile.Value);
//            }
//            _update = true;
//        }


//        public Tile GetTile(Point2i point) {
//            long pointL = point.AsLong();
//            if(!possibleTiles.Contains(pointL)) return null;
//            Tile tile;
//            if(tiles.TryGetValue(pointL, out tile) == false) {
//                tile = new Tile(this, point);
//                tiles.TryAdd(pointL, tile);
//            }
//            return tile;
//        }
//        public int TileCount() => tiles.Count;

//        public TileShadeFrames GetTileShadeFrame(Point2i point) {
//            long pointL = point.AsLong();
//            TileShadeFrames fr;
//            if(!shadeFrames.TryGetValue(pointL, out fr)) {
//                fr = new TileShadeFrames(this, point);
//                shadeFrames.TryAdd(pointL, fr);
//            }
//            return fr;
//        }

//        public bool Empty() => possibleTiles.Count == 0;

//        public int ShadeTiles() => tiles.Values.Where(t => t.shade.IsActive).Count();

//        public int ShadeFrames() => shadeFrames.Values.Select(t => t.frames.Count).Sum();
//    }

//    public class Tile {
//        public TileShade shade;
//        public List<GenDataEditor> editors;


//        public void RegisterRedraw() { 
//            map.drawTilePool.RegisterRedo(this);
//        }

//        public readonly Point2i pos;
//        private readonly TileMap map;
//        public Tile(TileMap tileMap, Point2i position) {
//            this.map = tileMap;
//            this.pos = position;
//            shade = new TileShade(this);

//            map.generateTilePool.RegisterRedo(this);
//            editors = [shade];
//        }

//        public void QueueGenerate(WorldPosition observer) {
//            map.generateTilePool.Queue(this, observer);
//        }
//        public void QueueDraw(ShaderPipeline gldrawer) {
//            //map.drawTilePool.Queue(this, () => {
//            //    this.Redraw();
//            //}, () => true);
//            gldrawer.OnRender(new WorldPosition(new Point(pos.X * 512, pos.Z * 512), 512, 512, 1), map, this.texture);
//            map.drawTilePool.curr[this] = map.drawTilePool.max[this];
//        }

//        public TileMap GetOrigin() { 
//            return map;
//        }




//        private GenData _genData;
//        public GenData genData {
//            get {
//                return _genData;
//            }
//            set {
//                _genData = value;
//                if(Global.App.Colormap.TintManager.GetBlendingTints().Any()) {
//                    for(int i = -1; i <= 1; i++) { // biome blend
//                        for(int j = -1; j <= 1; j++) {
//                            var tile = map.GetTile(pos + new Point2i(i, j));
//                            if(tile == null) continue;
//                            if(tile.genData != null) tile.RegisterRedraw();
//                        }
//                    }
//                } else if(Global.Settings.SHADETYPE == ShadeType.jmap) {
//                    this.RegisterRedraw();

//                    var tile = map.GetTile(pos + Global.Settings.Jmap_MAP_DIRECTION switch { 
//                        Direction.North => new Point2i(0, 1),
//                        Direction.South => new Point2i(0, -1),
//                        Direction.East => new Point2i(-1, 0),
//                        Direction.West => new Point2i(1, 0),
//                    });
//                    if(tile != null) {
//                        tile.RegisterRedraw();
//                    }
                    
//                } else this.RegisterRedraw();
//            }
//        }
//        private WriteableBitmap _img;
//        public WriteableBitmap img {
//            get {
//                return _img;
//            }
//            set {
//                _img = value;
//            }
//        }

//        private int _texture = -1;
//        public int texture {
//            get {
//                if (_texture == -1) {
//                    _texture = GL.GenTexture();
//                    GL.BindTexture(TextureTarget.Texture2D, _texture);
//                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
//                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

//                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, 512, 512, 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);
//                }
//                return _texture;
//            }
//        }



//        //public unsafe void Redraw() {
//        //    if(this.genData == null) return;
//        //    var img = new WriteableBitmap(512, 512, 96, 96, PixelFormats.Bgra32, null);
//        //    img.Lock();

//        //    uint* pixels = (uint*)img.BackBuffer;
//        //    GenData[,] neighbours = new GenData[3, 3];
//        //    neighbours[1, 1] = genData;
//        //    if(Global.App.Colormap.TintManager.GetBlendingTints().Where(Global.App.Colormap.Grouping.HaveInRecord).Any() || Global.Settings.OCEAN_DEPTH_BLENDING > 1) {
//        //        for(int i = -1; i <= 1; i++) { // biome blend
//        //            for(int j = -1; j <= 1; j++) {
//        //                var tile = map.GetTile(pos + new Point2i(i, j));
//        //                if(tile == null) continue;
//        //                neighbours[i + 1, j + 1] = tile.genData;
//        //            }
//        //        }
//        //    } else if(Global.Settings.SHADETYPE == ShadeType.jmap) {
//        //        Point2i p = Global.Settings.Jmap_MAP_DIRECTION switch {
//        //            Direction.North => new Point2i(0, -1),
//        //            Direction.South => new Point2i(0, 1),
//        //            Direction.East => new Point2i(1, 0),
//        //            Direction.West => new Point2i(-1, 0),
//        //        };
//        //        var tile = map.GetTile(pos + p);
//        //        if(tile != null) {
//        //            neighbours[p.X + 1, p.Z + 1] = tile.genData;
//        //        }
//        //    }



//        //    var tempgen = genData.GetTempInstance();
//        //    TileDraw.FillPixels(pixels, Global.App.Colormap, tempgen, neighbours);
//        //    tempgen.DisposeTemporal();

//        //    img.Unlock();
//        //    img.Freeze();

//        //    this.img = img;

//        //}
//    }
//}
