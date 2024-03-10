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


        enum AllocOption { Null, New, Rent }
        class RegArrayFactory<T> where T : struct {
            public readonly ArrayPool<T> arrayPool;
            public readonly int sizePerRegion;
            public readonly T def;
            public readonly AllocOption allocOption;
            public RegArrayFactory(bool create, int sizePerRegion, AllocOption options, T def = default) {
                this.sizePerRegion = sizePerRegion;

                if(create == false) this.allocOption = AllocOption.Null;
                else this.allocOption = options;

                this.def = def;

                if(allocOption == AllocOption.Rent) {
                    arrayPool = ArrayPool<T>.Create(sizePerRegion, Settings.MAXCONCURRENCY);
                }
            }

            public bool IsCreated() => allocOption != AllocOption.Null;

            public ManArray<T> Create() {
                if(allocOption == AllocOption.Rent) return new ManArray<T>(arrayPool, sizePerRegion, def);
                else if(allocOption == AllocOption.New) return new ManArray<T>(sizePerRegion, def);
                else return new ManArray<T>(def);
            } 
            //public RegArray<T> CreateNew() => new RegArray<T>(this, RegArray<T>.AllocOption.New);
        }
        public class ManArray<T> : IDisposable where T : struct {
            private readonly AllocOption way;
            private T[] array;
            private readonly int size;
            private T def;
            private ArrayPool<T> pool = null;

            public ManArray(ArrayPool<T> pool, int size, T def = default) {
                this.pool = pool;
                this.size = size;
                this.array = pool.Rent(size);
                this.way = AllocOption.Rent;
                this.def = def;
            }
            public ManArray(int size, T def = default) {
                this.array = new T[size];
                this.size = size;
                this.way = AllocOption.New;
                this.def = def;
            }
            public ManArray(T def = default) {
                this.array = null;
                this.size = 0;
                this.way = AllocOption.Null;
                this.def = def;
            }
            public ManArray(ManArray<T> arr) {
                this.array = arr.Get().DeepCopy();
                this.size = arr.Length;
                this.way = AllocOption.New;
                this.def = arr.def;
            }


            public T[] Get() => this.array;
            public bool IsNull() => way == AllocOption.Null;
            public T this[int i] {
                get {
                    if(way == AllocOption.Null) return this.def;
                    //if(i >= array.Length || i < 0) return this.def;
                    return array[i];
                }
                set { if(array != null) array[i] = value; }
            }
            public int Length { get => size; }
            public static implicit operator T[](ManArray<T> d) => d.array;
            public static implicit operator Span<T>(ManArray<T> d) => d.array;

            public ManArray<T> Free() {
                if(way == AllocOption.Null) return this;
                else if(way == AllocOption.Rent) return new ManArray<T>(this);
                else if(way == AllocOption.New) return this;
                return null;
            }

            public void Dispose() {
                if(way == AllocOption.Rent) {
                    this.pool.Return(array, true);
                }
            }
        }

        public readonly ArrayPool<int> chunk_biomes;
        public readonly ArrayPool<long> blockstates;
        public readonly ArrayPool<long> ocean_floor, world_surface;

        public class RawData : IDisposable {
            public ManArray<ushort> blockIds;
            public ManArray<ushort> biomeIds;
            public ManArray<short> heights;
            public ManArray<short> terrainHeights;
            public ManArray<bool> shadeFrame;
            public ManArray<bool> shadeValues;
            public ManArray<byte> shadeValuesLen;

            public RawData(GenerateTilePool pool) {
                blockIds = pool.blockIdsInit.Create();

                biomeIds = pool.biomeIdsInit.Create();

                heights = pool.heightsInit.Create();

                terrainHeights = pool.terrainHeightsInit.Create();

                shadeFrame = pool.shadeFrameInit.Create();
                shadeValues = pool.shadeValuesInit.Create();
                shadeValuesLen = pool.shadeValuesLenInit.Create();
            }
            private RawData() { }

            public RawData Free() {
                return new RawData() { 
                    blockIds = this.blockIds.Free(),
                    biomeIds = this.biomeIds.Free(),
                    heights = this.heights.Free(),
                    terrainHeights = this.terrainHeights.Free(),
                    shadeFrame = this.shadeFrame.Free(),
                    shadeValues= this.shadeValues.Free(),
                    shadeValuesLen = this.shadeValuesLen.Free(),
                };
            }

            public void Dispose() {
                blockIds.Dispose();
                biomeIds.Dispose();
                heights.Dispose();
                terrainHeights.Dispose();
                shadeFrame.Dispose();
            }
        }
        public class GenData {
            public ManArray<ushort> blockIds;
            public ManArray<ushort> biomeIds;
            public ManArray<short> heights;
            public ManArray<short> terrainHeights;
            public ManArray<bool> isShade;

            public GenData(RawData rawData, GenerateTilePool pool) {
                blockIds = rawData.blockIds;
                biomeIds = rawData.biomeIds;
                heights = rawData.heights;
                terrainHeights = rawData.terrainHeights;

                isShade = pool.isShadeInit.Create();
            }
        }

        private readonly RegArrayFactory<uint> pixelBufferInit;
        public ManArray<uint> BorrowPixels() => pixelBufferInit.Create();

        private readonly RegArrayFactory<ushort> blockIdsInit;
        private readonly RegArrayFactory<ushort> biomeIdsInit;
        private readonly RegArrayFactory<short> heightsInit;
        private readonly RegArrayFactory<short> terrainHeightsInit;
        private readonly RegArrayFactory<bool> shadeFrameInit;
        private readonly RegArrayFactory<bool> shadeValuesInit;
        private readonly RegArrayFactory<byte> shadeValuesLenInit;

        private readonly RegArrayFactory<bool> isShadeInit;

        public GenerateTilePool() : base(Settings.MAXCONCURRENCY) {

            pixelBufferInit = new RegArrayFactory<uint>(true, 512 * 512, AllocOption.Rent);

            var allocOptions = AllocOption.Rent;
            if(Settings.SHADE3D) allocOptions = AllocOption.New;

            blockIdsInit = new RegArrayFactory<ushort>(true, 512 * 512, allocOptions);
            biomeIdsInit = new RegArrayFactory<ushort>(Settings.BIOMES, 512 * 512, allocOptions);
            heightsInit = new RegArrayFactory<short>(Settings.WATER || Settings.SHADE3D || Settings.STATIC_SHADE, 512 * 512, allocOptions);
            terrainHeightsInit = new RegArrayFactory<short>(Settings.WATER, 512 * 512, allocOptions);
            shadeFrameInit = new RegArrayFactory<bool>(Settings.SHADE3D, (ShadeConstants.GLB.rX * 512) * (ShadeConstants.GLB.rZ * 512), allocOptions);
            shadeValuesInit = new RegArrayFactory<bool>(Settings.SHADE3D, 512 * 512 * ShadeConstants.GLB.blockReachLenMax, allocOptions);
            shadeValuesLenInit = new RegArrayFactory<byte>(Settings.SHADE3D, 512 * 512, allocOptions);

            isShadeInit = new RegArrayFactory<bool>(shadeValuesInit.IsCreated(), 512 * 512, shadeValuesInit.allocOption);

            chunk_biomes = ArrayPool<int>.Create(1536, Settings.MAXCONCURRENCY * Settings.CHUNKRENDERMAXCONCURRENCY);
            ocean_floor = ArrayPool<long>.Create(37, Settings.MAXCONCURRENCY * Settings.CHUNKRENDERMAXCONCURRENCY);
            world_surface = ArrayPool<long>.Create(37, Settings.MAXCONCURRENCY * Settings.CHUNKRENDERMAXCONCURRENCY);
            blockstates = ArrayPool<long>.Create(768, Settings.MAXCONCURRENCY * 24 * Settings.CHUNKRENDERMAXCONCURRENCY);
        }



        public static long redrawAcc = 0, redrawCount = 1;
        public void Queue(Tile tile, WorldPosition observer) {
            if(observers.ContainsKey(tile) == false) observers.TryAdd(tile, new List<WorldPosition>());

            if(observers[tile].Contains(observer) == false) observers[tile].Add(observer);

            base.Queue(tile, () => {
                Global.Time(() => {
                    tile.image.Generate();
                }, out var time);
                redrawAcc += time;
                redrawCount++;
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