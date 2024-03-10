using System;
using System.Collections.Concurrent;
using System.Threading;

namespace AsyncLoggers.Utilities
{
    public class AsyncWrapper
    {
        public delegate void LogCallback();
        private delegate bool RunCondition();

        private readonly Thread _loggingThread;
        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentQueue<LogCallback> _tasks;
        private static readonly RunCondition _defaultCondition = ()=>true;
        private volatile RunCondition _shouldRun = _defaultCondition;

        internal AsyncWrapper()
        {
            _tasks = new ConcurrentQueue<LogCallback>();
            _semaphore = new SemaphoreSlim(0);
            _loggingThread = new Thread(LogWorker)
            {
                IsBackground = true
            };
            _loggingThread.Start();
        }
        
        private void LogWorker()
        {
            while (_shouldRun())
            {
                try
                {
                    if (!_semaphore.Wait(1000)) 
                        continue;
                    if (_tasks.TryDequeue(out var task))
                    {
                        task();
                    }
                }
                catch (ThreadInterruptedException)
                {
                    _shouldRun = ()=>false;
                }
            }
        }

        public void Schedule(LogCallback callback)
        {
            if (_shouldRun != _defaultCondition) 
                return;
            
            _tasks.Enqueue(callback);
            _semaphore.Release();
        }

        public void Stop(bool immediate=false)
        {
            if (immediate)
            {
                _shouldRun = ()=>false;
                _loggingThread.Interrupt();
            }else
                _shouldRun = ()=>_tasks.Count > 0;
        }
    }
}