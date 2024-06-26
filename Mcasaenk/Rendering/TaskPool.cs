using Mcasaenk.Shade3d;
using Mcasaenk.UI.Canvas;
using Microsoft.VisualBasic;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
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
        protected TaskScheduler task_pool;
        public Pool(int maxConcurrency) {
            this.maxConcurrency = maxConcurrency;

            //task_pool = new LimitedConcurrencyLevelTaskScheduler(maxConcurrency);
            task_pool = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Current, maxConcurrency).ConcurrentScheduler;
        }

        public void QueueTask(Task task) {
            task.Start(task_pool);
        }
        public int GetLoadingQueue() {
            if(task_pool is LimitedConcurrencyLevelTaskScheduler l) return l.TaskCount();
            else return -1;
        }
    }

    public class TilePool : Pool {
        private readonly ConcurrentDictionary<Tile, byte> queued, loading;
        private ConcurrentDictionary<Tile, int> max, curr;
        public TilePool(int maxConcurrency) : base(maxConcurrency) {
            queued = new ConcurrentDictionary<Tile, byte>();
            loading = new ConcurrentDictionary<Tile, byte>();

            max = new ConcurrentDictionary<Tile, int>();
            curr = new ConcurrentDictionary<Tile, int>();
        }

        public void Queue(Tile tile, Action f, Func<bool> finalCheck, TaskCreationOptions taskCreationOptions = TaskCreationOptions.None) {
            if(queued.ContainsKey(tile)) return;
            queued.TryAdd(tile, default);
            Task task = new Task(() => {
                try {
                    loading.TryAdd(tile, default);
                    if(!finalCheck()) return;

                    int c = max[tile];
                    f();
                    curr[tile] = c;
                } finally {
                    queued.TryRemove(tile, out _);
                    loading.TryRemove(tile, out _);
                }
            }, taskCreationOptions);
            base.QueueTask(task);
        }
        public bool IsQueued(Tile tile) => queued.ContainsKey(tile);
        public bool IsLoading(Tile tile) => loading.ContainsKey(tile);

        public bool ShouldDo(Tile tile) {
            if(curr.TryGetValue(tile, out var v) == false) return false;
            return max[tile] > curr[tile];
        }
        public void RegisterRedo(Tile tile) {
            if(curr.ContainsKey(tile)) {
                max[tile] = max[tile] + 1;
            } else { 
                max.TryAdd(tile, 1);
                curr.TryAdd(tile, 0);
            }
        }
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
                    tile.genData = Global.App.Settings.SHADE3D ? TileGenerate.ShadeGenerate(tile, TaskScheduler.Default) : TileGenerate.StandartGenerate(tile);
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
                return atleastone;
            }, TaskCreationOptions.LongRunning);
        }

    }
}