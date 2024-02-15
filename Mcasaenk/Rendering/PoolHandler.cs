using Mcasaenk.Shade3d;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcasaenk.Rendering {
    public class PoolHandler {
        private static LimitedConcurrencyLevelTaskScheduler task_pool = new LimitedConcurrencyLevelTaskScheduler(Settings.MAXCONCURRENCY);

        public static readonly ArrayPool<int> pixelBuffer = ArrayPool<int>.Create(512 * 512, Settings.MAXCONCURRENCY);
        public static readonly ArrayPool<int> waterPixels = ArrayPool<int>.Create(512 * 512, Settings.MAXCONCURRENCY);
        public static readonly ArrayPool<short> terrainHeights = ArrayPool<short>.Create(512 * 512, Settings.MAXCONCURRENCY);
        public static readonly ArrayPool<short> waterHeights = ArrayPool<short>.Create(512 * 512, Settings.MAXCONCURRENCY);

        public static readonly ArrayPool<int> biomes = ArrayPool<int>.Create(1536, Settings.MAXCONCURRENCY * 1024);
        public static readonly ArrayPool<long> blockstates = ArrayPool<long>.Create(768, Settings.MAXCONCURRENCY * 1024 * 24);

        public static readonly ArrayPool<bool> shades = ArrayPool<bool>.Create((ShadeConstants.GLOBAL.rX * 512) * (ShadeConstants.GLOBAL.rZ * 512), Settings.MAXCONCURRENCY);

        public static void StartLoadingTask(Task task) {
            task.Start(task_pool);
        }

        public static int GetLoadingQueue() {
            return task_pool.TaskCount();
        }
    }
}
