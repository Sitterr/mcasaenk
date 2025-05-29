namespace Mcasaenk.Rendering {
    public class TaskPool {
        protected readonly int maxConcurrency;
        protected TaskScheduler task_pool;
        public TaskPool(int maxConcurrency) {
            this.maxConcurrency = maxConcurrency;

            task_pool = new LimitedConcurrencyLevelTaskScheduler(maxConcurrency);
            //task_pool = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Current, maxConcurrency).ConcurrentScheduler;
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