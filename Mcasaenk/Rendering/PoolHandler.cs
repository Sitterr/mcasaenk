using Mcasaenk.Shade3d;
using Mcasaenk.UI.Canvas;
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
        private Dictionary<Tile, List<WorldPosition>> observers = new();



        public class RegArrayFactory<T> where T : struct {
            public readonly ArrayPool<T> arrayPool;
            public readonly int sizePerRegion;
            public readonly bool isCreated;
            public readonly T def;
            public RegArrayFactory(bool create, int sizePerRegion, T def = default) {
                this.sizePerRegion = sizePerRegion;
                this.isCreated = create;
                this.def = def;
                if(isCreated) {
                    arrayPool = ArrayPool<T>.Create(sizePerRegion, Settings.MAXCONCURRENCY);
                }
            }

            public ManArray<T> CreateRent() {
                if(isCreated) return new ManArray<T>(arrayPool, sizePerRegion, def);
                else return new ManArray<T>(def);
            } 
            //public RegArray<T> CreateNew() => new RegArray<T>(this, RegArray<T>.AllocOption.New);
        }
        public class ManArray<T> : IDisposable where T : struct {
            public enum AllocOption { Null, New, Rent }

            private readonly AllocOption way;
            private readonly T[] array;
            private T def;
            private ArrayPool<T> pool = null;

            public ManArray(ArrayPool<T> pool, int size, T def = default) {
                this.pool = pool;
                this.array = pool.Rent(size);
                this.way = AllocOption.Rent;
                this.def = def;
            }
            public ManArray(int size, T def = default) {
                this.array = new T[size];
                this.way = AllocOption.New;
                this.def = def;
            }
            public ManArray(T def = default) {
                this.array = null;
                this.way = AllocOption.Null;
                this.def = def;
            }
            public ManArray(T[] arr, T def = default) {
                this.array = arr.DeepCopy();
                this.way = AllocOption.New;
                this.def = def;
            }


            public T[] Get() => this.array;
            public bool IsNull() => way == AllocOption.Null;
            public  T this[int i] {
                get { return way != AllocOption.Null ? array[i] : this.def; }
                set { array[i] = value; }
            }
            public static implicit operator T[](ManArray<T> d) => d.array;
            public static implicit operator Span<T>(ManArray<T> d) => d.array;

            public ManArray<T> Free() {
                if(way == AllocOption.Null) return this;
                else if(way == AllocOption.Rent) return new ManArray<T>(this.array, this.def);
                else if(way == AllocOption.New) return this;
                return null;
            }

            public void Dispose() {
                if(way == AllocOption.Rent) {
                    this.pool.Return(array, true);
                }
                GC.SuppressFinalize(this);
            }
        }

        public readonly ArrayPool<int> chunk_biomes;
        public readonly ArrayPool<long> blockstates;

        public record RawData : IDisposable {
            public ManArray<ushort> blockIds;
            public ManArray<ushort> biomeIds;
            public ManArray<short> heights;
            public ManArray<short> terrainHeights;
            public ManArray<bool> shadeFrame;
            public ManArray<bool> shadeValues;
            public ManArray<byte> shadeValuesLen;

            public RawData(GenerateTilePool pool) {
                blockIds = pool.blockIdsInit.CreateRent();

                biomeIds = pool.biomeIdsInit.CreateRent();

                heights = pool.heightsInit.CreateRent();

                terrainHeights = pool.terrainHeightsInit.CreateRent();

                shadeFrame = pool.shadeFrameInit.CreateRent();
                shadeValues = pool.shadeValuesInit.CreateRent();
                shadeValuesLen = pool.shadeValuesLenInit.CreateRent();
            }
            private RawData() { }

            public RawData Free() {
                return new RawData() { 
                    blockIds = this.blockIds.Free(),
                    biomeIds = this.biomeIds.Free(),
                    heights = this.heights.Free(),
                    terrainHeights = this.terrainHeights.Free(),
                    shadeFrame = this.shadeFrame.Free(),
                };
            }

            public void Dispose() {
                blockIds.Dispose();
                biomeIds.Dispose();
                heights.Dispose();
                terrainHeights.Dispose();
                shadeFrame.Dispose();

                GC.SuppressFinalize(this);
            }
        }
        public record GenData {
            public ManArray<ushort> blockIds;
            public ManArray<ushort> biomeIds;
            public ManArray<short> heights;
            public ManArray<short> terrainHeights;
            public ManArray<bool> isShade;

            public GenData(RawData rawData) {
                blockIds = rawData.blockIds;
                biomeIds = rawData.biomeIds;
                heights = rawData.heights;
                terrainHeights = rawData.terrainHeights;
                isShade = new ManArray<bool>(512 * 512);
            }
        }

        public readonly RegArrayFactory<uint> pixelBufferInit;
        public readonly RegArrayFactory<ushort> blockIdsInit;
        public readonly RegArrayFactory<ushort> biomeIdsInit;
        public readonly RegArrayFactory<short> heightsInit;
        public readonly RegArrayFactory<short> terrainHeightsInit;
        public readonly RegArrayFactory<bool> shadeFrameInit;
        public readonly RegArrayFactory<bool> shadeValuesInit;
        public readonly RegArrayFactory<byte> shadeValuesLenInit;

        public GenerateTilePool() : base(Settings.MAXCONCURRENCY) {
            pixelBufferInit = new RegArrayFactory<uint>(true, 512 * 512);
            blockIdsInit = new RegArrayFactory<ushort>(true, 512 * 512);
            biomeIdsInit = new RegArrayFactory<ushort>(Settings.BIOMES, 512 * 512);
            heightsInit = new RegArrayFactory<short>(Settings.WATER || Settings.SHADE3D || Settings.STATIC_SHADE, 512 * 512);
            terrainHeightsInit = new RegArrayFactory<short>(Settings.WATER, 512 * 512);
            shadeFrameInit = new RegArrayFactory<bool>(Settings.SHADE3D, (ShadeConstants.GLB.rX * 512) * (ShadeConstants.GLB.rZ * 512));
            shadeValuesInit = new RegArrayFactory<bool>(Settings.SHADE3D, 512 * 512 * ShadeConstants.GLB.blockReachLenMax);
            shadeValuesLenInit = new RegArrayFactory<byte>(Settings.SHADE3D, 512 * 512);

            chunk_biomes = ArrayPool<int>.Create(1536, Settings.MAXCONCURRENCY * Settings.CHUNKRENDERMAXCONCURRENCY);
            blockstates = ArrayPool<long>.Create(768, Settings.MAXCONCURRENCY * 24 * Settings.CHUNKRENDERMAXCONCURRENCY);
        }

        public static long lastRedrawTime = 0;

        public void Queue(Tile tile, WorldPosition observer) {
            if(observers.ContainsKey(tile) == false) observers.Add(tile, new List<WorldPosition>());

            if(observers[tile].Contains(observer) == false) observers[tile].Add(observer);

            base.Queue(tile, () => {
                Global.Time(() => {
                    tile.image.Generate();
                }, out lastRedrawTime);
                observers[tile].Clear();
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

    }
}