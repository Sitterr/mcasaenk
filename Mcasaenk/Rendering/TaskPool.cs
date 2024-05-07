using Mcasaenk.Shade3d;
using Mcasaenk.UI.Canvas;
using Microsoft.VisualBasic;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using static Mcasaenk.Rendering.GenerateTilePool;

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
        private readonly ConcurrentDictionary<Tile, byte> queued, loading, loaded;
        public TilePool(int maxConcurrency) : base(maxConcurrency) {
            queued = new ConcurrentDictionary<Tile, byte>();
            loading = new ConcurrentDictionary<Tile, byte>();
            loaded = new ConcurrentDictionary<Tile, byte>();
        }

        public void Queue(Tile tile, Action f, Func<bool> finalCheck) {
            if(queued.ContainsKey(tile)) return;
            queued.TryAdd(tile, default);
            Task task = new Task(() => {
                try {
                    loading.TryAdd(tile, default);
                    if(!finalCheck()) return;

                    f();
                    loaded.TryAdd(tile, default);
                }
                finally {
                    loading.TryRemove(tile, out _);
                    queued.TryRemove(tile, out _);
                }
            });
            base.QueueTask(task);
        }

        public bool IsLoading(Tile tile) => loading.ContainsKey(tile);
        public bool IsQueued(Tile tile) => queued.ContainsKey(tile);
        public bool HasLoaded(Tile tile) => loaded.ContainsKey(tile);
    }

    public class GenerateTilePool : TilePool {
        private ConcurrentDictionary<Tile, List<WorldPosition>> observers = new();

        public GenerateTilePool() : base(Global.App.Settings.MAXCONCURRENCY) {  }

        public static long redrawAcc = 0, redrawCount = 1;
        public void Queue(Tile tile, WorldPosition observer) {
            if(observers.ContainsKey(tile) == false) observers.TryAdd(tile, new List<WorldPosition>());

            if(observers[tile].Contains(observer) == false) observers[tile].Add(observer);

            base.Queue(tile, (Action)(() => {
                Global.Time((Action)(() => {
                    tile.genData = Global.App.Settings.SHADE3D ? TileGenerate.ShadeGenerate(tile) : TileGenerate.StandartGenerate(tile);
                }), out var time);
                redrawAcc += time;
                redrawCount++;
                observers[tile].Clear();
            }), () => {
                bool atleastone = false;
                foreach(var screen in observers[tile]) {
                    if(screen.IsVisible(tile)) {
                        atleastone = true;
                        break;
                    }
                }
                if(tile.IsRedrawing()) return false;
                return atleastone;
            });
        }

    }
}