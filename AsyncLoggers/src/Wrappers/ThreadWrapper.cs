﻿using System;
using System.Threading;
using DisruptorUnity3d;

namespace AsyncLoggers.Wrappers
{
    public class ThreadWrapper: IAsyncWrapper
    {
        private delegate bool RunCondition();

        private readonly Thread _loggingThread;
        private readonly SemaphoreSlim _semaphore;
        //private readonly ConcurrentQueue<LogCallback> _tasks;
        private readonly RingBuffer<IAsyncWrapper.LogCallback> _taskRingBuffer;
        private static readonly RunCondition DefaultCondition = ()=>true;
        private volatile RunCondition _shouldRun = DefaultCondition;

        internal ThreadWrapper()
        {
            //_tasks = new ConcurrentQueue<LogCallback>();
            _taskRingBuffer = new RingBuffer<IAsyncWrapper.LogCallback>(AsyncLoggers.PluginConfig.Scheduler.ThreadBufferSize.Value);
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
                    //if (_tasks.TryDequeue(out var task))
                    if (_taskRingBuffer.TryDequeue(out var task))
                    {
                        task?.Invoke();
                    }
                }
                catch (ThreadInterruptedException)
                {
                    _shouldRun = () => false;
                }
                catch (Exception ex)
                {
                    AsyncLoggers.Log.LogError($"Exception while logging: {ex}");
                }
            }
        }

        public void Schedule(IAsyncWrapper.LogCallback callback)
        {
            if (_shouldRun != DefaultCondition) 
                return;
            
            //_tasks.Enqueue(callback);
            _taskRingBuffer.Enqueue(callback);
            _semaphore.Release();
        }

        public void Stop(bool immediate=false)
        {
            if (immediate)
            {
                _shouldRun = () => false;
                _loggingThread.Interrupt();
            }
            else
                //_shouldRun = ()=>_tasks.Count > 0;
                _shouldRun = () => _taskRingBuffer.Count > 0;
        }
    }
}