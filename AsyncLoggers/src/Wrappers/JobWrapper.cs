using System;
using System.Threading;
using DisruptorUnity3d;
using Unity.Jobs;

namespace AsyncLoggers.Wrappers
{
    
    public class JobWrapper: IAsyncWrapper
    {
        private static readonly RunCondition DefaultCondition = ()=>SINGLETON._taskRingBuffer.Count > 0;
        internal static readonly JobWrapper SINGLETON = new JobWrapper();
        
        private delegate bool RunCondition();

        private JobHandle? _loggingJob;
        private readonly LogJob _loggingJobStruct;
        private readonly ConcurrentCircularBuffer<IAsyncWrapper.LogCallback> _taskRingBuffer;
        private volatile RunCondition _shouldRun;

        private JobWrapper()
        {
            _taskRingBuffer = new ConcurrentCircularBuffer<IAsyncWrapper.LogCallback>(AsyncLoggers.PluginConfig.Scheduler.JobBufferSize.Value);
            _shouldRun = DefaultCondition;
            _loggingJobStruct = new LogJob();
        }

        public void Schedule(IAsyncWrapper.LogCallback callback)
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
                try
                {
                    AsyncLoggers.Log.LogError($"Bad Exception while logging: {ex}");}
                catch (Exception)
                {
                    Console.WriteLine($"Exception while logging: {ex}");
                }
            }
        }
    }
}