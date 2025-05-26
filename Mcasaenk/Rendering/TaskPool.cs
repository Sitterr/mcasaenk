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

namespace Mcasaenk.Rendering {
    public class TaskPool {
        protected readonly int maxConcurrency;
        protected TaskScheduler task_pool;
        public TaskPool(int maxConcurrency) {
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

}