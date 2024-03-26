using System;
using System.Collections.Generic;
using System.Threading;
using AsyncLoggers.StaticContexts;
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
        internal static IWrapper.ContextType ContextType { get; private set; }

        public JobWrapper(IWrapper.ContextType contextType = IWrapper.ContextType.Log)
        {
            var _id = Interlocked.Add(ref _IDSeed, 1);
            INSTANCES[_id] = this;
            ContextType = contextType;
            _taskRingBuffer = new ConcurrentCircularBuffer<IWrapper.LogCallback>(PluginConfig.Scheduler.JobBufferSize.Value);
            DefaultCondition = ()=>_taskRingBuffer.Count > 0;
            _shouldRun = DefaultCondition;
            _loggingJobStruct = new LogJob(_id);
        }

        
        private long getUUID()
        {
            return ContextType switch
            {
                IWrapper.ContextType.Log => LogContext.Uuid!.Value,
                IWrapper.ContextType.Event => EventContext.Uuid!.Value,
                _ => -1L
            };
        }
        
        public void Schedule(IWrapper.LogCallback callback)
        {
            if (_shouldRun != DefaultCondition) 
                return;
            
            var uuid = getUUID();
            var timestamp = GenericContext.Timestamp;

            _taskRingBuffer.Enqueue(CallbackWrapper);

            if (_loggingJob == null || _loggingJob.Value.IsCompleted )
            {
                _loggingJob = _loggingJobStruct.Schedule();
            }
            
            void CallbackWrapper()
            {
                try
                {
                    GenericContext.Async = true;
                    GenericContext.Timestamp = timestamp;
                    switch (ContextType)
                    {
                        case IWrapper.ContextType.Log:
                            LogContext.Uuid = uuid;
                            break;
                        case IWrapper.ContextType.Event:
                            EventContext.Uuid = uuid;
                            break;
                    }
                    callback();
                }
                finally
                {
                    GenericContext.Async = false;
                    GenericContext.Timestamp = null;
                    LogContext.Uuid = null;
                    EventContext.Uuid = null;
                }
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
                    catch (ThreadInterruptedException)
                    {
                        _shouldRun = () => false;
                    }
                    catch (Exception ex)
                    {
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