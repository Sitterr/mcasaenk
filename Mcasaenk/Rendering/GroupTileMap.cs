using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using Mcasaenk.Shade3d;
using Mcasaenk.UI.Canvas;

namespace Mcasaenk.Rendering {
    public class TileMapQuerer<T> {
        protected readonly GroupTileMap<T> tilemap;
        public TileMapQuerer(GroupTileMap<T> tilemap) {
            this.tilemap = tilemap;
        }
        public virtual void QueueDo(Point2i p) { tilemap.Do(p, default); }
    }
    public class ObserverTaskTileMapQueuer<T> : TileMapQuerer<T>, IDisposable {
        private readonly ConcurrentDictionary<Point2i, byte> queued, loading;
        private readonly TaskPool taskPool;
        private readonly TaskCreationOptions taskCreationOptions;

        private readonly CancellationTokenSource cancelToken;

        public ObserverTaskTileMapQueuer(GroupTileMap<T> tilemap, int maxConcurrency, TaskCreationOptions taskCreationOptions = TaskCreationOptions.None) : base(tilemap) {
            cancelToken = new CancellationTokenSource();
            taskPool = new TaskPool(maxConcurrency);

            this.taskCreationOptions = taskCreationOptions;

            queued = new ConcurrentDictionary<Point2i, byte>();
            loading = new ConcurrentDictionary<Point2i, byte>();
        }
        public void Dispose() {
            cancelToken.Cancel();
        }

        private ConcurrentDictionary<string, WorldPosition> observers = new();
        public void Observer(string name, WorldPosition screen) {
            observers[name] = screen;
        }

        public override void QueueDo(Point2i p) {
            if(queued.ContainsKey(p)) return;
            queued.TryAdd(p, default);

            Task task = new Task(() => {
                try {
                    loading.TryAdd(p, default);

                    {
                        bool atleastone = false;
                        foreach(var screen in observers.Values) {
                            if(tilemap.Scope(p).InterSects(screen)) {
                                atleastone = true;
                                break;
                            }
                        }
                        if(atleastone == false) {
                            return;
                        }
                    }

                    tilemap.Do(p, cancelToken.Token);

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

    public static class TileMap {
        public static IEnumerable<Point2i> GetVisibleTilesPositions(WorldPosition screen, int TileSize) {
            double sx = Math.Floor(Math.Floor(screen.Start.X) / TileSize), sz = Math.Floor(Math.Floor(screen.Start.Y) / TileSize), tx = Global.Coord.absMod(screen.Start.X, TileSize), tz = Global.Coord.absMod(screen.Start.Y, TileSize);
            for(int x = 0; x * TileSize - tx < screen.Width; x++) {
                for(int z = 0; z * TileSize - tz < screen.Height; z++) {
                    yield return new Point2i(x + sx, z + sz);
                }
            }
        }

        public static (Point2i min, Point2i max) GetVisibleRect(WorldPosition screen, int TileSize) {
            double sx = Math.Floor(Math.Floor(screen.Start.X) / TileSize), sz = Math.Floor(Math.Floor(screen.Start.Y) / TileSize);
            double fx = Math.Floor(Math.Ceiling(screen.Start.X + screen.Width - 1) / TileSize), fz = Math.Floor(Math.Ceiling(screen.Start.Y + screen.Height - 1) / TileSize);

            return (new Point2i(sx, sz), new Point2i(fx, fz));
        }
    }

    public abstract class GroupTileMap<Tile> : IDisposable {
        private readonly Stack<Tile> recycleStack;
        private IDictionary<Point2i, Tile> tiles;
        protected bool wasUsedForRecycle = false;

        protected double bundlebr;
        protected double scale;
        public virtual double ID => bundlebr;

        protected TileMapQuerer<Tile> queuer;

        public GroupTileMap(double bundlebr, double scale, bool doallbydefault, bool concurrent, GroupTileMap<Tile> oldTileMap) {
            recycleStack = new Stack<Tile>(oldTileMap?.ID == this.ID ? oldTileMap?.UseForRecycle() : []);

            this.bundlebr = bundlebr;
            this.scale = scale;

            if(concurrent) {
                max = new ConcurrentDictionary<Point2i, int>();
                curr = new ConcurrentDictionary<Point2i, int>();
                tiles = new ConcurrentDictionary<Point2i, Tile>();
            } else {
                max = new Dictionary<Point2i, int>();
                curr = new Dictionary<Point2i, int>();
                tiles = new Dictionary<Point2i, Tile>();
            }

            queuer = new TileMapQuerer<Tile>(this);

            minmax = doallbydefault ? 1 : 0;
        }
        protected void SetQueuer(TileMapQuerer<Tile> queuer) { this.queuer = queuer; }

        public int TileSize => (int)(512 * bundlebr / scale);
        public int TileSizeR => (int)(512 * bundlebr);
        public WorldPosition Scope(Point2i p) {
            return new WorldPosition(new System.Windows.Point(p.X * TileSize, p.Z * TileSize), TileSize, TileSize, scale);
        }

        public IEnumerable<Point2i> GetVisibleTilesPositions(WorldPosition screen) => TileMap.GetVisibleTilesPositions(screen, TileSize);
        public IEnumerable<Point2i> GetVisibleTilesPositions(WorldPosition[] screens) {
            foreach(var screen in screens) {
                foreach(var p in GetVisibleTilesPositions(screen)) {
                    yield return p;
                }
            }
        }
        public (Point2i min, Point2i max) GetVisibleRect(WorldPosition screen) => TileMap.GetVisibleRect(screen, TileSize);

        public bool GetTile(Point2i p, out Tile tile) {
            return tiles.TryGetValue(p, out tile);
        }
        public Tile GetTile(Point2i p) {
            if(GetTile(p, out var tile)) return tile;
            else return default;
        }
        private Tile GetOrCreateTile(Point2i p) {
            if(GetTile(p, out var tile)) return tile;
            if(recycleStack.Count > 0) return recycleStack.Pop();
            return CreateTile();
        }
        public void Observer(string name, WorldPosition screen) {
            if(queuer is ObserverTaskTileMapQueuer<Tile> q) q.Observer(name, screen);
        }



        private int minmax = 0;
        private IDictionary<Point2i, int> max, curr;
        public int GetTileState(Point2i p) {
            if(curr.TryGetValue(p, out int v)) return v;
            return -1;
        }
        protected bool ShouldDo(Point2i p) {
            if(curr.TryGetValue(p, out int c) == false) return minmax > 0;
            if(max.TryGetValue(p, out int m) == false) return false;
            return m > c;
        }
        protected void Redo(Point2i p) {
            if(curr.ContainsKey(p)) {
                max[p] = max[p] + 1;
            } else {
                max.TryAdd(p, 1);
                curr.TryAdd(p, 0);
            }
        }



        private int timebr, timeacc;
        public int MeanDoTime() => timebr > 0 ? timeacc / timebr : 0;

        public void Do(Point2i p, CancellationToken cancellationToken) {
            Stopwatch st = Stopwatch.StartNew();

            int c = max.GetValueOrDefault(p, minmax);
            var cr = GetOrCreateTile(p);
            var val = _Do(p, cr, cancellationToken);
            if(!EqualityComparer<Tile>.Default.Equals(cr, val) && cr is IDisposable dcr) dcr.Dispose();


            curr[p] = c;
            if(max.ContainsKey(p) == false) max[p] = c;

            tiles[p] = val;

            st.Stop();
            timebr++;
            timeacc += (int)st.ElapsedMilliseconds;
        }
        protected abstract Tile _Do(Point2i p, Tile tile, CancellationToken cancellationToken);

        public virtual void MassRedo() {
            curr.Clear();
            max.Clear();
        }

        protected void Reset() {
            foreach(var t in tiles) {
                recycleStack.Push(t.Value);
            }
            tiles.Clear();
            MassRedo();

            timebr = 0;
            timeacc = 0;
        }

        protected abstract Tile CreateTile();



        public virtual void Dispose() {
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
        protected abstract void DisposeTile(Tile tile);
        private bool disposed = false;

        public Tile[] UseForRecycle() {
            if(wasUsedForRecycle) return [];
            wasUsedForRecycle = true;
            this.Dispose();
            return recycleStack.ToArray().Concat(tiles.Values).ToArray();
        }

    }

    public interface DrawGroupTileMap {
        void DoVisible(KeyValuePair<string, WorldPosition> visiblescreen, KeyValuePair<string, WorldPosition>[] movingextras, bool quickscan);
        void Reset(GenDataTileMap gentilemap = null);
        void MassRedo();
        void OnScaleChange(double scale);

        int MeanDoTime();
    }

    public abstract class DrawGroupTileMap<Tile> : GroupTileMap<Tile>, DrawGroupTileMap {
        protected GenDataTileMap gentilemap;
        private readonly IDictionary<Point2i, int[]> gendepend;
        protected readonly Dictionary<string, WorldPosition> extras;

        private List<Point2i> depend;
        private int[] empt;
        private int primarydep;
        public DrawGroupTileMap(GenDataTileMap gentilemap, double bundlebr, double scale, bool concurrent, GroupTileMap<Tile> oldTileMap) : base(bundlebr, Math.Min(1, scale), false, concurrent, oldTileMap) {
            extras = new Dictionary<string, WorldPosition>();

            if(concurrent) {
                gendepend = new ConcurrentDictionary<Point2i, int[]>();
            } else {
                gendepend = new Dictionary<Point2i, int[]>();
            }

            Reset(gentilemap);
            OnScaleChange(scale);
        }

        private bool ShouldDo(Point2i p, bool quickscan) {
            if(gentilemap == null) return false;
            if(base.ShouldDo(p)) return true;

            int[] states = gendepend.GetValueOrDefault(p, empt);
            Point2i min = gentilemap.GetVisibleRect(this.Scope(p)).min;
            if(states == empt || quickscan) {
                for(int i = 0; i < primarydep; i++) {
                    if(states[i] < gentilemap.GetTileState(depend[i] + min)) return true;
                }
                return false;
            }

            for(int i = 0; i < depend.Count; i++) {
                if(states[i] < gentilemap.GetTileState(depend[i] + min)) return true;
            }
            return false;
        }

        public virtual void DoVisible(KeyValuePair<string, WorldPosition> visiblescreen, KeyValuePair<string, WorldPosition>[] movingextras, bool quickscan) {
            foreach(var me in movingextras) {
                WorldPosition old = new WorldPosition();
                if(this.extras.TryGetValue(me.Key, out var wp)) {
                    old = wp;
                }
                if(old != me.Value) {
                    foreach(var tile in this.GetVisibleTilesPositions(old)) Redo(tile);
                    foreach(var tile in this.GetVisibleTilesPositions(me.Value)) Redo(tile);
                }
                extras[me.Key] = me.Value;
            }

            base.Observer(visiblescreen.Key, visiblescreen.Value);
            foreach(var tile in this.GetVisibleTilesPositions(visiblescreen.Value)) {
                if(ShouldDo(tile, quickscan)) {
                    queuer.QueueDo(tile);
                }
            }
        }

        public override void MassRedo() {
            base.MassRedo();
            gendepend.Clear();
        }

        public void Reset(GenDataTileMap gentilemap = null) {
            base.Reset();
            if(gentilemap != null) this.gentilemap = gentilemap;
            gendepend.Clear();
            extras.Clear();
            depend = GetDependencies();
            empt = new int[depend.Count];
        }

        public virtual void OnScaleChange(double scale) {
            if(scale > 0) this.scale = Math.Min(1, scale);
        }

        protected override Tile _Do(Point2i p, Tile tile, CancellationToken _) {
            int[] states = gendepend.GetValueOrDefault(p, new int[depend.Count]);
            Point2i min = gentilemap.GetVisibleRect(this.Scope(p)).min;
            for(int i = 0; i < depend.Count; i++) {
                states[i] = gentilemap.GetTileState(depend[i] + min);
            }
            if(gendepend.ContainsKey(p) == false) gendepend[p] = states;

            return __Do(p, tile);
        }
        protected abstract Tile __Do(Point2i p, Tile tile);

        private List<Point2i> GetDependencies() {
            List<Point2i> list = new();
            if(gentilemap == null) return list;
            var (min, max) = gentilemap.GetVisibleRect(this.Scope(new Point2i(0, 0)));
            int w = max.Z - min.Z + 1;
            primarydep = 0;
            for(int z = min.Z; z <= max.Z; z++) {
                for(int x = min.X; x <= max.X; x++) {
                    Point2i b = new Point2i(x, z);

                    list.Insert(0, b - min);
                    primarydep++;

                    foreach(var r in ShadeConstants.GLB.regionReach) {
                        list.Add(b - r.p - min);
                    }
                }
            }
            foreach(var ext in gentilemap.GetVisibleTilesPositions(this.Scope(new Point2i(0, 0)).Extend(512))) {
                list.Add(ext - min);
            }

            return list.Distinct().ToList();
        }
    }

    public class GenDataTileMap : GroupTileMap<GenData> {
        public ArrayPool<short> heightPool;
        public ArrayPool<short> depthsPool;
        public ArrayPool<ushort> blockIdsPool;
        public ArrayPool<ushort> biomeIds8_light4_shade4Pool;

        public readonly Dimension dim;
        private readonly HashSet<Point2i> existingRegions;

        private ConcurrentDictionary<Point2i, TileShadeFrames> shadeFrames;
        private ConcurrentDictionary<Point2i, TileShade> shadesTiles;
        public TileShade GetShadeTile(Point2i p) {
            if(shadesTiles.TryGetValue(p, out var tile)) return tile;
            if(existingRegions.Contains(p) == false) return null;

            shadesTiles[p] = new TileShade(this, p);
            return shadesTiles[p];
        }
        public TileShadeFrames GetTileShadeFrame(Point2i p) {
            if(shadeFrames.TryGetValue(p, out var tile)) return tile;

            var f = new TileShadeFrames(this, p);
            shadeFrames[p] = f;
            return f;
        }
        public int ShadeTiles() => shadesTiles.Values.Where(t => t.IsActive).Count();
        public int ShadeFrames() => shadeFrames.Values.Select(t => t.frames.Count).Sum();

        public GenDataTileMap(Dimension dimension, HashSet<Point2i> existingRegions) : base(1, 1, true, true, null) {
            this.dim = dimension;
            this.existingRegions = existingRegions;

            shadeFrames = new ConcurrentDictionary<Point2i, TileShadeFrames>();
            shadesTiles = new ConcurrentDictionary<Point2i, TileShade>();

            int maxConcurrency = Global.Settings.MAXCONCURRENCY;
            heightPool = ArrayPool<short>.Create(512 * 512, maxConcurrency * (Global.Settings.TRANSPARENTLAYERS + 1));
            depthsPool = ArrayPool<short>.Create(512 * 512, maxConcurrency * (Global.Settings.TRANSPARENTLAYERS + 1));
            blockIdsPool = ArrayPool<ushort>.Create(512 * 512, maxConcurrency * (Global.Settings.TRANSPARENTLAYERS + 1));
            biomeIds8_light4_shade4Pool = ArrayPool<ushort>.Create(512 * 512, maxConcurrency * (Global.Settings.TRANSPARENTLAYERS + 1));

            SetQueuer(new ObserverTaskTileMapQueuer<GenData>(this, maxConcurrency));
        }

        public bool IsLoading(Point2i p) => ((ObserverTaskTileMapQueuer<GenData>)queuer).IsLoading(p);
        public bool IsQueued(Point2i p) => ((ObserverTaskTileMapQueuer<GenData>)queuer).IsQueued(p);

        public IEnumerable<(Point2i reg, Point2i chunk)> GetVisibleChunkPositions(WorldPosition screen) {
            foreach(var ch in TileMap.GetVisibleTilesPositions(screen, 16)) {
                yield return (new Point2i(ch.X, ch.Z) / 32, new Point2i(ch.X, ch.Z) % 32);
            }
        }


        public bool RegionExists(Point2i p) {
            return existingRegions.Contains(p);
        }

        public void DoVisible(KeyValuePair<string, WorldPosition> visiblescreen) {
            base.Observer(visiblescreen.Key, visiblescreen.Value);
            foreach(var tile in this.GetVisibleTilesPositions(visiblescreen.Value)) {
                if(ShouldDo(tile)) {
                    queuer.QueueDo(tile);
                }
            }
        }

        private bool ShouldDo(Point2i p) {
            if(RegionExists(p) == false) return false;
            return base.ShouldDo(p);
        }
        protected override GenData _Do(Point2i p, GenData _, CancellationToken cancellationToken) {
            try {
                var v = (Global.App.Settings.SHADETYPE == ShadeType.OG && Global.App.Settings.SHADE3D) ? TileGenerate.ShadeGenerate(this, p, dim.GetRegionPath(p), cancellationToken) : TileGenerate.StandartGenerate(this, dim.GetRegionPath(p), cancellationToken);
                return v;
            } catch {
                return null;
            }
        }

        protected override void DisposeTile(GenData tile) {
            tile?.Dispose();
        }
        protected override GenData CreateTile() => null;

        public override void Dispose() {
            base.Dispose();
            (queuer as ObserverTaskTileMapQueuer<GenData>).Dispose();
        }
    }

}
