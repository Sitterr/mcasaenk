using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Mcasaenk.Shade3d;
using Mcasaenk.Shaders;
using Mcasaenk.UI.Canvas;
using OpenTK.Graphics.OpenGL4;
using static Mcasaenk.Global;

namespace Mcasaenk.Rendering {
    public abstract class GroupTileMap : IDisposable {

        protected double bundlebr;
        protected double scale;
        public virtual double ID => bundlebr;

        protected TileMapQuerer querer;

        public GroupTileMap(double bundlebr, double scale, bool doallbydefault) {
            this.bundlebr = bundlebr;
            this.scale = scale;

            this.querer = new TileMapQuerer(this);

            max = new ConcurrentDictionary<Point2i, int>();
            curr = new ConcurrentDictionary<Point2i, int>();

            minmax = doallbydefault ? 1 : 0;
        }
        public void SetQuerer(TileMapQuerer querer) {
            this.querer = querer;
        }

        public int TileSize => (int)(512 * bundlebr / scale);
        public int TileSizeL => (int)(512 * bundlebr);
        public WorldPosition Scope(Point2i p) {
            return new WorldPosition(new System.Windows.Point(p.X * TileSize, p.Z * TileSize), TileSize, TileSize, scale);
        }

        protected static IEnumerable<Point2i> GetVisibleTilesPositions(WorldPosition screen, int TileSize) {
            double sx = Math.Floor(Math.Floor(screen.Start.X) / TileSize), sz = Math.Floor(Math.Floor(screen.Start.Y) / TileSize), tx = Global.Coord.absMod(screen.Start.X, TileSize), tz = Global.Coord.absMod(screen.Start.Y, TileSize);
            for(int x = 0; x * TileSize - tx < screen.Width; x++) {
                for(int z = 0; z * TileSize - tz < screen.Height; z++) {
                    yield return new Point2i(x + sx, z + sz);
                }
            }
        }
        public IEnumerable<Point2i> GetVisibleTilesPositions(WorldPosition screen) => GetVisibleTilesPositions(screen, TileSize);
        public IEnumerable<Point2i> GetVisibleTilesPositions(WorldPosition[] screens) {
            foreach(var screen in screens) {
                foreach(var p in GetVisibleTilesPositions(screen)) { 
                    yield return p;
                }
            }
        }

        public (Point2i min, Point2i max) GetVisibleRect(WorldPosition screen) {
            double sx = Math.Floor(Math.Floor(screen.Start.X) / TileSize), sz = Math.Floor(Math.Floor(screen.Start.Y) / TileSize);
            double fx = Math.Floor(Math.Ceiling(screen.Start.X + screen.Width - 1) / TileSize), fz = Math.Floor(Math.Ceiling(screen.Start.Y + screen.Height - 1) / TileSize);

            return (new Point2i(sx, sz), new Point2i(fx, fz));
        }

        public abstract void Dispose();

        private int minmax = 0;
        private readonly ConcurrentDictionary<Point2i, int> max, curr;
        protected abstract void _Do(Point2i p);
        public void Do(Point2i p) {
            int c = minmax;
            bool maxexists = max.TryGetValue(p, out c);

            curr[p] = c;
            if (maxexists == false) max[p] = c;
            
            this._Do(p);
        }

        public void QueueDo(Point2i p, WorldPosition observer) {
            querer.QueueDo(p, observer);
        }

        public virtual bool ShouldDo(Point2i p) {
            if (curr.TryGetValue(p, out int c) == false) return minmax > 0;
            if (max.TryGetValue(p, out int m) == false) return false;
            return m > c;
        }
        public void Redo(Point2i p) {
            if (curr.ContainsKey(p)) {
                max[p] = max[p] + 1;
            } else {
                max.TryAdd(p, 1);
                curr.TryAdd(p, 0);
            }
        }

        public void MassRedo() {
            foreach (var p in curr.Keys) {
                Redo(p);
            }
        }
    }

    public class TileMapQuerer {
        protected readonly GroupTileMap tilemap;
        public TileMapQuerer(GroupTileMap tilemap) {
            this.tilemap = tilemap;
        }
        public virtual void QueueDo(Point2i p, WorldPosition observer) { tilemap.Do(p); }
    }
    public class ObserverTaskTileMapQueuer : TileMapQuerer {
        private readonly ConcurrentDictionary<Point2i, byte> queued, loading;
        private readonly TaskPool taskPool;
        private readonly TaskCreationOptions taskCreationOptions;

        private ConcurrentDictionary<Point2i, List<WorldPosition>> observers = new();

        public ObserverTaskTileMapQueuer(GroupTileMap tilemap, int maxConcurrency, TaskCreationOptions taskCreationOptions = TaskCreationOptions.None) : base(tilemap) {
            taskPool = new TaskPool(maxConcurrency);

            this.taskCreationOptions = taskCreationOptions;

            queued = new ConcurrentDictionary<Point2i, byte>();
            loading = new ConcurrentDictionary<Point2i, byte>();
        }

        public override void QueueDo(Point2i p, WorldPosition observer) {
            if (queued.ContainsKey(p)) return;
            queued.TryAdd(p, default);

            if (observers.ContainsKey(p) == false) observers.TryAdd(p, new List<WorldPosition>());
            if (observers[p].Contains(observer) == false) observers[p].Add(observer);

            Task task = new Task(() => {
                try {
                    loading.TryAdd(p, default);

                    {
                        bool atleastone = false;
                        foreach (var screen in observers[p]) {
                            if (tilemap.Scope(p).InterSects(screen)) {
                                atleastone = true;
                                break;
                            }
                        }
                        if (atleastone == false) return;
                    }

                    tilemap.Do(p);
                    observers[p].Clear();

                } finally {
                    queued.TryRemove(p, out _);
                    loading.TryRemove(p, out _);
                }
            }, taskCreationOptions);

            taskPool.QueueTask(task);
        }
        public bool IsQueued(Point2i p) => queued.ContainsKey(p);
        public bool IsLoading(Point2i p) => loading.ContainsKey(p);
    }



    public abstract class GroupTileMap<Tile> : GroupTileMap {
        private readonly Stack<Tile> recycleStack;
        private readonly ConcurrentDictionary<Point2i, Tile> tiles;
        protected bool wasUsedForRecycle = false;

        public GroupTileMap(double bundlebr, double scale, bool doallbydefault, GroupTileMap<Tile> oldTileMap) : base(bundlebr, scale, doallbydefault) {
            recycleStack = new Stack<Tile>(oldTileMap?.ID == this.ID ? oldTileMap?.UseForRecycle() : []);
            tiles = new ConcurrentDictionary<Point2i, Tile>();
        }

        public bool GetTile(Point2i p, out Tile tile) {
            return tiles.TryGetValue(p, out tile);
        }
        public Tile GetTile(Point2i p) {
            if (GetTile(p, out var tile)) return tile;
            else return default;
        }
        private Tile GetOrCreateTile(Point2i p) {
            if (GetTile(p, out var tile)) return tile;
            if (recycleStack.Count > 0) return recycleStack.Pop();
            return CreateTile();
        }

        protected override void _Do(Point2i p) {
            var val = __Do(p, GetOrCreateTile(p));

            if (tiles.TryGetValue(p, out var v)) {
                if (!EqualityComparer<Tile>.Default.Equals(v, val)) {
                    DisposeTile(tiles[p]);
                }
            }

            tiles[p] = val;           
        }

        protected abstract Tile __Do(Point2i p, Tile tile);


        protected abstract Tile CreateTile();
        protected abstract void DisposeTile(Tile tile);

        public override void Dispose() {
            if(!disposed) {
                if(wasUsedForRecycle == false) {
                    foreach(var e in recycleStack) {
                        DisposeTile(e);
                    }
                    foreach(var e in tiles) {
                        DisposeTile(e.Value);
                    }
                }
                disposed = true;
            }
        }
        private bool disposed = false;

        public Tile[] UseForRecycle() {
            if (wasUsedForRecycle) return [];
            wasUsedForRecycle = true;
            return recycleStack.ToArray().Concat(tiles.Values).ToArray();
        }

    }

    public class GenDataTileMap : GroupTileMap<GenData> {
        public ArrayPool<short> heightPool;
        public ArrayPool<short> depthsPool;
        public ArrayPool<ushort> blockIdsPool;
        public ArrayPool<ushort> biomeIds8_light4_shade4Pool;

        private readonly Dimension dim;
        private readonly HashSet<Point2i> existingRegions;
        private readonly List<GroupTileMap> drawTilemaps;


        private Dictionary<Point2i, TileShadeFrames> shadeFrames;
        private Dictionary<Point2i, TileShade> shadesTiles;
        public TileShade GetShadeTile(Point2i p) {
            if (shadesTiles.TryGetValue(p, out var tile)) return tile;
            if(existingRegions.Contains(p) == false) return null;

            shadesTiles[p] = new TileShade(this, p);
            return shadesTiles[p];
        }
        public TileShadeFrames GetTileShadeFrame(Point2i p) {
            if (shadeFrames.TryGetValue(p, out var tile)) return tile;

            var f = new TileShadeFrames(this, p);
            shadeFrames[p] = f;
            return f;
        }

        public GenDataTileMap(Dimension dimension, HashSet<Point2i> existingRegions) : base(1, 1, true, null) {
            this.dim = dimension;
            this.existingRegions = existingRegions;
            drawTilemaps = new List<GroupTileMap>();

            shadeFrames = new Dictionary<Point2i, TileShadeFrames>();
            shadesTiles = new Dictionary<Point2i, TileShade>();

            int maxConcurrency = Global.Settings.MAXCONCURRENCY;
            heightPool = ArrayPool<short>.Create(512 * 512, maxConcurrency * (Global.Settings.TRANSPARENTLAYERS + 1));
            depthsPool = ArrayPool<short>.Create(512 * 512, maxConcurrency * (Global.Settings.TRANSPARENTLAYERS + 1));
            blockIdsPool = ArrayPool<ushort>.Create(512 * 512, maxConcurrency * (Global.Settings.TRANSPARENTLAYERS + 1));
            biomeIds8_light4_shade4Pool = ArrayPool<ushort>.Create(512 * 512, maxConcurrency * (Global.Settings.TRANSPARENTLAYERS + 1));

            SetQuerer(new ObserverTaskTileMapQueuer(this, maxConcurrency));
        }
        public void AddDrawTileMap(GroupTileMap tilemap) { drawTilemaps.Add(tilemap); }
        public void RemoveDrawTileMap(GroupTileMap tilemap) { drawTilemaps.Remove(tilemap); }

        public bool IsLoading(Point2i p) => ((ObserverTaskTileMapQueuer)querer).IsLoading(p);
        public bool IsQueued(Point2i p) => ((ObserverTaskTileMapQueuer)querer).IsQueued(p);

        public IEnumerable<(Point2i reg, Point2i chunk)> GetVisibleChunkPositions(WorldPosition screen) {
            foreach(var ch in GetVisibleTilesPositions(screen, 16)) {
                yield return (new Point2i(ch.X, ch.Z) / 32, new Point2i(ch.X, ch.Z) % 32);
            }
        }

        public void RedoDrawTilemap(Point2i p, bool extend) {
            WorldPosition scope = extend ? this.Scope(p).Extend(512) : this.Scope(p);
            Global.App.Dispatcher.Invoke(() => {
                foreach(var tilemap in drawTilemaps) {
                    foreach(var dt in tilemap.GetVisibleTilesPositions(scope)) {
                        tilemap.Redo(dt);
                    }
                }
            });
        }

        public override bool ShouldDo(Point2i p) {
            if (!base.ShouldDo(p)) return false;
            else return RegionExists(p);
        }


        public bool RegionExists(Point2i p) {
            return existingRegions.Contains(p);
        }


        protected override GenData __Do(Point2i p, GenData _) {
            if (!RegionExists(p)) return null;
            var v = (Global.App.Settings.SHADETYPE == ShadeType.OG && Global.App.Settings.SHADE3D) ? TileGenerate.ShadeGenerate(this, p, dim.GetRegionPath(p)) : TileGenerate.StandartGenerate(this, dim.GetRegionPath(p));
            RedoDrawTilemap(p, true);
            //Thread.Sleep(1000 * Global.rand.Next(3, 15));
            return v;
        }

        protected override void DisposeTile(GenData tile) {
            tile?.Dispose();
        }
        protected override GenData CreateTile() => null;
    }

    public class OpenGLDrawTileMap : GroupTileMap<int> {
        private readonly GenDataTileMap gentilemap;
        private readonly ShaderPipeline gldrawer;

        private int emptyTile;
        public int GetEmptyTile() => emptyTile;

        public OpenGLDrawTileMap(GenDataTileMap gentilemap, ShaderPipeline gldrawer, double bundlebr, double scale, OpenGLDrawTileMap oldTileMap) : base(bundlebr, Math.Min(scale, 1), true, oldTileMap) {
            this.gentilemap = gentilemap;
            this.gldrawer = gldrawer;

            if(this.ID == oldTileMap?.ID) emptyTile = oldTileMap.emptyTile;
            else emptyTile = CreateTile();

            gentilemap.RemoveDrawTileMap(oldTileMap);
            gentilemap.AddDrawTileMap(this);

            oldTileMap?.Dispose();
        }
        public override bool ShouldDo(Point2i p) {
            if (!base.ShouldDo(p)) return false;
            else {
                foreach (var gt in gentilemap.GetVisibleTilesPositions(this.Scope(p))) {
                    if (gentilemap.GetTile(gt, out var g)) {
                        return true;
                    }
                }
                return false;
            }
        }

        protected override int __Do(Point2i p, int texture) {
            gldrawer.Render(Scope(p), gentilemap, texture);
            return texture;
        }

        protected override int CreateTile() {
            int texture = GL.GenTexture();

            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, TileSizeL, TileSizeL, 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);

            return texture;
        }

        protected override void DisposeTile(int tile) {
            if(tile != 0) GL.DeleteTexture(tile);
        }       
        public override void Dispose() {
            if(!disposed){
                disposed = true;
                base.Dispose();
                if(!wasUsedForRecycle) {
                    DisposeTile(emptyTile);
                }
            }
        }
        bool disposed = false;

    }


}
