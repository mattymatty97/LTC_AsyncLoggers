using System;
using System.Collections.Generic;
using System.Threading;
using DisruptorUnity3d;
using Unity.Jobs;
using UnityEngine;

namespace AsyncLoggers.Wrappers
{
    
    public class JobWrapper: IWrapper
    {
        private static long _IDSeed = 0;
        private readonly RunCondition DefaultCondition;
        internal static readonly Dictionary<long,JobWrapper> INSTANCES = new Dictionary<long, JobWrapper>();
        
        private delegate bool RunCondition();

        private JobHandle? _loggingJob;
        private readonly LogJob _loggingJobStruct;
        private readonly ConcurrentCircularBuffer<IWrapper.LogCallback> _taskRingBuffer;
        private volatile RunCondition _shouldRun;

        public JobWrapper()
        {
            var _id = Interlocked.Add(ref _IDSeed, 1);
            INSTANCES[_id] = this;
            _taskRingBuffer = new ConcurrentCircularBuffer<IWrapper.LogCallback>(PluginConfig.Scheduler.JobBufferSize.Value);
            DefaultCondition = ()=>_taskRingBuffer.Count > 0;
            _shouldRun = DefaultCondition;
            _loggingJobStruct = new LogJob(_id);
        }
        
        public void Schedule(IWrapper.LogCallback callback)
        {
            if (_shouldRun != DefaultCondition) 
                return;

            _taskRingBuffer.Enqueue(callback);

            if (_loggingJob == null || _loggingJob.Value.IsCompleted )
            {
                _loggingJob = _loggingJobStruct.Schedule();
            }
        }

        public void Stop(bool immediate=false)
        {
            if (immediate)
            {
                _shouldRun = () => false;
            }
            else
                _shouldRun = () => _taskRingBuffer.Count > 0;
        }
        
        private readonly struct LogJob: IJob
        {
            private readonly long _id;

            public LogJob(long id)
            {
                _id = id;
            }

            public void Execute()
            {
                INSTANCES[_id].LogWorker();
            }
        }
        
        
        [HideInCallstack]
        private void LogWorker()
        {
            try
            {
                while (_shouldRun())
                {
                    try
                    {
                        if (_taskRingBuffer.TryDequeue(out var task))
                        {
                            task?.Invoke();
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is ThreadInterruptedException || ex is ThreadAbortException)
                        {
                            _shouldRun = () => false;
                            break;
                        }
                        try
                        {
                            AsyncLoggerPreloader.Log.LogError($"Exception while logging: {ex}");
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
                if (ex is ThreadInterruptedException || ex is ThreadAbortException)
                {
                    _shouldRun = () => false;
                    return;
                }
                try
                {
                    AsyncLoggerPreloader.Log.LogError($"Bad Exception while logging: {ex}");}
                catch (Exception)
                {
                    Console.WriteLine($"Exception while logging: {ex}");
                }
            }
        }
    }
}