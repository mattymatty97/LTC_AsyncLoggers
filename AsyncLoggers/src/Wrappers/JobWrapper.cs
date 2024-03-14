﻿using System;
using System.Threading;
using DisruptorUnity3d;
using Unity.Jobs;

namespace AsyncLoggers.Wrappers
{
    
    public class JobWrapper: IAsyncWrapper
    {
        private static readonly RunCondition DefaultCondition = ()=>true;
        internal static readonly JobWrapper SINGLETON = new JobWrapper();
        
        private delegate bool RunCondition();

        private readonly JobHandle _loggingJob;
        private readonly SemaphoreSlim _semaphore;
        private readonly Buffer<IAsyncWrapper.LogCallback> _taskBuffer;
        private volatile RunCondition _shouldRun;

        private JobWrapper()
        {
            _taskBuffer = new Buffer<IAsyncWrapper.LogCallback>(200);
            _semaphore = new SemaphoreSlim(0);
            _shouldRun = DefaultCondition;
            _loggingJob = new LogJob().Schedule();
        }

        public void Schedule(IAsyncWrapper.LogCallback callback)
        {
            if (_shouldRun != DefaultCondition) 
                return;
            
            _taskBuffer.Enqueue(callback);
            _semaphore.Release();
        }

        public void Stop(bool immediate=false)
        {
            if (immediate)
            {
                _shouldRun = () => false;
            }
            else
                _shouldRun = () => _taskBuffer.Count > 0;
        }
        
        private struct LogJob: IJob
        {
            public void Execute()
            {
                JobWrapper.SINGLETON.LogWorker();
            }
        }
        

        private void LogWorker()
        {
            try
            {
                while (_shouldRun())
                {
                    try
                    {
                        if (!_semaphore.Wait(1000))
                            continue;
                        //if (_tasks.TryDequeue(out var task))
                        if (_taskBuffer.TryDequeue(out var task))
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
                        try
                        {
                            AsyncLoggers.Log.LogError($"Exception while logging: {ex}");
                        }
                        catch (Exception)
                        {
                            Console.WriteLine($"Exception while logging: {ex}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AsyncLoggers.Log.LogError($"Bad Exception while logging: {ex}");
            }
        }
    }
}