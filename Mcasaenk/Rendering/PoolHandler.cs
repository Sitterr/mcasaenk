using Mcasaenk.Shade3d;
using Mcasaenk.UI.Canvas;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Animation;

namespace Mcasaenk.Rendering {
    public class Pool {
        protected readonly int maxConcurrency;
        private LimitedConcurrencyLevelTaskScheduler task_pool;
        public Pool(int maxConcurrency) {
            this.maxConcurrency = maxConcurrency;

            task_pool = new LimitedConcurrencyLevelTaskScheduler(maxConcurrency);
        }

        public void QueueTask(Task task) {
            task.Start(task_pool);
        }
        public int GetLoadingQueue() {
            return task_pool.TaskCount();
        }
    }

    public class TilePool : Pool {
        private readonly HashSet<Tile> queued, loading;
        public TilePool(int maxConcurrency) : base(maxConcurrency) {
            queued = new HashSet<Tile>();
            loading = new HashSet<Tile>();
        }

        public void QueueTileTask(Tile tile, Action<TilePool> f, Func<bool> finalCheck) {
            if(queued.Contains(tile)) return;
            queued.Add(tile);
            Task task = new Task(() => {
                try {
                    loading.Add(tile);
                    if(!finalCheck()) return;

                    f(this);
                }
                finally {
                    loading.Remove(tile);
                    queued.Remove(tile);
                }
            });
            base.QueueTask(task);
        }

        public bool IsLoading(Tile tile) => loading.Contains(tile);
        public bool IsQueued(Tile tile) => queued.Contains(tile);
    }

    public abstract class GenerateTilePool : TilePool {
        private readonly HashSet<Tile> loaded;
        private Dictionary<Tile, List<WorldPosition>> observers;

        public readonly ArrayPool<int> pixelBuffer;

        public readonly ArrayPool<int> biomes;
        public readonly ArrayPool<long> blockstates;

        public readonly int parallelChunksPerRegion;

        public GenerateTilePool() : base(Settings.MAXCONCURRENCY) {
            parallelChunksPerRegion = Settings.CHUNKRENDERMAXCONCURRENCY;

            loaded = new HashSet<Tile>();
            observers = new Dictionary<Tile, List<WorldPosition>>();

            pixelBuffer = ArrayPool<int>.Create(512 * 512, maxConcurrency);

            biomes = ArrayPool<int>.Create(1536, maxConcurrency * parallelChunksPerRegion);
            blockstates = ArrayPool<long>.Create(768, maxConcurrency * 24 * parallelChunksPerRegion);
        }

        public void Queue(Tile tile, WorldPosition observer) {
            if(observers.ContainsKey(tile) == false) observers.Add(tile, new List<WorldPosition>());

            if(observers[tile].Contains(observer) == false) observers[tile].Add(observer);

            base.QueueTileTask(tile, (_) => {
                this.Generate(tile);
                loaded.Add(tile);
            }, () => {
                bool atleastone = false;
                foreach(var screen in observers[tile]) {
                    if(screen.IsVisible(tile)) {
                        atleastone = true;
                        break;
                    }
                }
                return atleastone;
            });
        }

        protected abstract void Generate(Tile tile);

        public bool HasLoaded(Tile tile) => loaded.Contains(tile);
    }

    public class StandardGenerateTilePool : GenerateTilePool {
        public readonly ArrayPool<int> waterPixels;
        public readonly ArrayPool<short> terrainHeights;
        public readonly ArrayPool<short> waterHeights;
        public StandardGenerateTilePool() : base() {
            waterPixels = ArrayPool<int>.Create(512 * 512, maxConcurrency);
            terrainHeights = ArrayPool<short>.Create(512 * 512, maxConcurrency);
            waterHeights = ArrayPool<short>.Create(512 * 512, maxConcurrency);
        }

        protected override void Generate(Tile tile) {
            Global.Time(() => {
                tile.image.StandartGenerate(this);
            }, out TileImage.lastRedrawTime);
            
        }
    }

    public class ShadeGenerateTilePool : GenerateTilePool {
        public readonly ArrayPool<int> waterPixels;
        public readonly ArrayPool<short> terrainHeights;

        public readonly ArrayPool<bool> shadeFrame;
        public ShadeGenerateTilePool() : base() {
            waterPixels = ArrayPool<int>.Create(512 * 512, maxConcurrency);
            terrainHeights = ArrayPool<short>.Create(512 * 512, maxConcurrency);

            shadeFrame = ArrayPool<bool>.Create((ShadeConstants.GLB.rX * 512) * (ShadeConstants.GLB.rZ * 512), maxConcurrency);
        }
        protected override void Generate(Tile tile) {
            Global.Time(() => {
                tile.image.ShadeGenerate(this);
            }, out TileImage.lastRedrawTime);
        }
    }
}
