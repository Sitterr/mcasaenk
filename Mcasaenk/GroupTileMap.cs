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
using System.Windows.Media.TextFormatting;
using Mcasaenk.Shade3d;
using Mcasaenk.Shaders;
using Mcasaenk.Shaders.Dissect;
using Mcasaenk.UI.Canvas;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.GraphicsLibraryFramework;
using static Mcasaenk.Global;

namespace Mcasaenk.Rendering {
    public class TileMapQuerer<T> {
        protected readonly GroupTileMap<T> tilemap;
        public TileMapQuerer(GroupTileMap<T> tilemap) {
            this.tilemap = tilemap;
        }
        public virtual void QueueDo(Point2i p, WorldPosition observer) { tilemap.Do(p); }
    }
    public class ObserverTaskTileMapQueuer<T> : TileMapQuerer<T> {
        private readonly ConcurrentDictionary<Point2i, byte> queued, loading;
        private readonly TaskPool taskPool;
        private readonly TaskCreationOptions taskCreationOptions;

        private ConcurrentDictionary<Point2i, List<WorldPosition>> observers = new();

        public ObserverTaskTileMapQueuer(GroupTileMap<T> tilemap, int maxConcurrency, TaskCreationOptions taskCreationOptions = TaskCreationOptions.None) : base(tilemap) {
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
                        if(atleastone == false) {
                            return;
                        }
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



    public abstract class GroupTileMap<Tile> : IDisposable {
        private readonly Stack<Tile> recycleStack;
        private readonly Dictionary<Point2i, Tile> tiles;
        protected bool wasUsedForRecycle = false;

        protected double bundlebr;
        protected double scale;
        public virtual double ID => bundlebr;

        protected TileMapQuerer<Tile> queuer;

        public GroupTileMap(double bundlebr, double scale, bool doallbydefault, GroupTileMap<Tile> oldTileMap) {
            recycleStack = new Stack<Tile>(oldTileMap?.ID == this.ID ? oldTileMap?.UseForRecycle() : []);
            tiles = new Dictionary<Point2i, Tile>();

            this.bundlebr = bundlebr;
            this.scale = scale;

            queuer = new TileMapQuerer<Tile>(this);

            max = new Dictionary<Point2i, int>();
            curr = new Dictionary<Point2i, int>();
            minmax = doallbydefault ? 1 : 0;
        }
        protected void SetQueuer(TileMapQuerer<Tile> queuer) { this.queuer = queuer; }

        public int TileSize => (int)(512 * bundlebr / scale);
        public int TileSizeR => (int)(512 * bundlebr);
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




        private int minmax = 0;
        private readonly Dictionary<Point2i, int> max, curr;
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





        public void Do(Point2i p) {
            int c = max.GetValueOrDefault(p, minmax);

            var cr = GetOrCreateTile(p);
            var val = _Do(p, cr);
            if(!EqualityComparer<Tile>.Default.Equals(cr, val) && cr is IDisposable dcr) dcr.Dispose();


            curr[p] = c;
            if(max.ContainsKey(p) == false) max[p] = c;

            tiles[p] = val;           
        }
        protected abstract Tile _Do(Point2i p, Tile tile);

        protected void MassClear() {        
            foreach(var t in tiles) { 
                recycleStack.Push(t.Value);
            }
            tiles.Clear();
            curr.Clear();
            max.Clear();
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
            if (wasUsedForRecycle) return [];
            wasUsedForRecycle = true;
            this.Dispose();
            return recycleStack.ToArray().Concat(tiles.Values).ToArray();
        }

    }

    public abstract class DrawGroupTileMap<Tile> : GroupTileMap<Tile> {
        protected GenDataTileMap gentilemap;
        private readonly Dictionary<Point2i, int[]> gendepend;
        protected readonly Dictionary<string, WorldPosition> extras;

        private List<Point2i> depend;
        private int[] empt;
        private int primarydep;
        public DrawGroupTileMap(GenDataTileMap gentilemap, double bundlebr, double scale, GroupTileMap<Tile> oldTileMap) : base(bundlebr, Math.Min(1, scale), false, oldTileMap) {
            gendepend = new Dictionary<Point2i, int[]>();
            extras = new Dictionary<string, WorldPosition>();
            MassRedo(scale, gentilemap);
        }

        private bool ShouldDo(Point2i p, bool quickscan) {
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

        public virtual void DoVisible(WorldPosition visiblescreen, KeyValuePair<string, WorldPosition>[] movingextras, bool quickscan) {
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

            foreach(var tile in this.GetVisibleTilesPositions(visiblescreen)) {
                if(ShouldDo(tile, quickscan)) {
                    queuer.QueueDo(tile, visiblescreen);
                }
            }
        }

        public void MassRedo(double scale = -1, GenDataTileMap gentilemap = null) {
            base.MassClear();
            if(gentilemap != null) this.gentilemap = gentilemap;
            if(scale > 0) this.scale = Math.Min(1, scale);
            gendepend.Clear();
            extras.Clear();
            depend = GetDependencies();
            empt = new int[depend.Count];
        }

        protected override Tile _Do(Point2i p, Tile tile) {
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

        private readonly Dimension dim;
        private readonly HashSet<Point2i> existingRegions;

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

            shadeFrames = new Dictionary<Point2i, TileShadeFrames>();
            shadesTiles = new Dictionary<Point2i, TileShade>();

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
            foreach(var ch in GetVisibleTilesPositions(screen, 16)) {
                yield return (new Point2i(ch.X, ch.Z) / 32, new Point2i(ch.X, ch.Z) % 32);
            }
        }


        public bool RegionExists(Point2i p) {
            return existingRegions.Contains(p);
        }

        public void DoVisible(WorldPosition visiblescreen) {
            foreach(var tile in this.GetVisibleTilesPositions(visiblescreen)) {
                if(ShouldDo(tile)) {
                    queuer.QueueDo(tile, visiblescreen);
                }
            }
        }

        private bool ShouldDo(Point2i p) {
            if(RegionExists(p) == false) return false;
            return base.ShouldDo(p);
        } 
        protected override GenData _Do(Point2i p, GenData _) {
            var v = (Global.App.Settings.SHADETYPE == ShadeType.OG && Global.App.Settings.SHADE3D) ? TileGenerate.ShadeGenerate(this, p, dim.GetRegionPath(p)) : TileGenerate.StandartGenerate(this, dim.GetRegionPath(p));
            return v;
        }

        protected override void DisposeTile(GenData tile) {
            tile?.Dispose();
        }
        protected override GenData CreateTile() => null;
    }

    public class OpenGLDrawTileMap : DrawGroupTileMap<int> {
        private readonly DissectShader dissectShader;
        private readonly ShaderPipeline gldrawer;
        public readonly int emptyTile;

        public OpenGLDrawTileMap(GenDataTileMap gentilemap, ShaderPipeline gldrawer, DissectShader dissectShader, double bundlebr, double scale, OpenGLDrawTileMap oldTileMap) : base(gentilemap, bundlebr, Math.Min(scale, 1), oldTileMap) {
            this.gldrawer = gldrawer;
            this.dissectShader = dissectShader;

            this.emptyTile = CreateTile();

            {
                thebigtexture = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, thebigtexture);
                //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, 500, 500, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
                //GL.BindTexture(TextureTarget.Texture2D, thebigtexture);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            }
        }

        HashSet<Point2i> todo = new();
        protected override int __Do(Point2i p, int texture) {
            todo.Add(p);
            // actual work in DoVisible();
            return texture;
        }

        public override void DoVisible(WorldPosition visiblescreen, KeyValuePair<string, WorldPosition>[] movingextras, bool quickscan) {
            todo.Clear();
            base.DoVisible(visiblescreen, movingextras, quickscan);

            if(todo.Count > 0) {
                Point2i min = new Point2i(int.MaxValue, int.MaxValue), max = new Point2i(int.MinValue, int.MinValue);
                foreach(var t in todo) {
                    min.X = Math.Min(min.X, t.X);
                    min.Z = Math.Min(min.Z, t.Z);

                    max.X = Math.Max(max.X, t.X);
                    max.Z = Math.Max(max.Z, t.Z);
                }

                Point2i bigsize = (max - min + 1) * TileSizeR;
                ResizeTheBigTexture(bigsize);
                gldrawer.Render(new WorldPosition(new Point(min.X * TileSize, min.Z * TileSize), bigsize.X / scale, bigsize.Z / scale, scale), gentilemap, extras.GetValueOrDefault("map_screenshot", (WorldPosition)default), thebigtexture);
                
                dissectShader.Use(thebigtexture, todo.Select(t => (t - min, GetTile(t))), new Point2i(TileSizeR, TileSizeR), bigsize);
            }
        }

        private int thebigtexture;
        private Point2i thebigtexture_size = new Point2i(0, 0);
        private void ResizeTheBigTexture(Point2i size) {
            if(size.X > thebigtexture_size.X || size.Z > thebigtexture_size.Z) {
                GL.DeleteTexture(thebigtexture);
                thebigtexture = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, thebigtexture);
                //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, 500, 500, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
                GL.TexStorage2D(TextureTarget2d.Texture2D, 1, SizedInternalFormat.Rgba8, size.X, size.Z);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

                thebigtexture_size = size;
            }
        }

        protected override int CreateTile() {
            int texture = GL.GenTexture();

            GL.BindTexture(TextureTarget.Texture2D, texture);

            GL.TexStorage2D(TextureTarget2d.Texture2D, 1, SizedInternalFormat.Rgba8, TileSizeR, TileSizeR);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            
            //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, TileSizeR, TileSizeR, 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);

            return texture;
        }

        protected override void DisposeTile(int tile) {
            if(tile != 0) GL.DeleteTexture(tile);
        }       
        public override void Dispose() {
            if(!disposed){
                disposed = true;
                base.Dispose();
                DisposeTile(emptyTile);
                GL.DeleteTexture(thebigtexture);
            }
        }
        bool disposed = false;

    }


}
