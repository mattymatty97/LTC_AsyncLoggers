using System;
using System.Collections.Generic;
using System.Threading;
using AsyncLoggers.Buffer;
using AsyncLoggers.Config;

namespace AsyncLoggers.Wrappers
{
    public class ThreadWrapper: IWrapper
    {
        internal static readonly HashSet<ThreadWrapper> Wrappers = [];
        private delegate bool RunCondition();

        private readonly Thread _loggingThread;
        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentCircularBuffer<Tuple<IWrapper.LogCallback,object, BepInEx.Logging.LogEventArgs>> _taskRingBuffer;
        private static readonly RunCondition DefaultCondition = ()=>true;
        private volatile RunCondition _shouldRun = DefaultCondition;

        internal ThreadWrapper(string threadName = nameof(ThreadWrapper))
        {
            _taskRingBuffer = new ConcurrentCircularBuffer<Tuple<IWrapper.LogCallback,object, BepInEx.Logging.LogEventArgs>>(PluginConfig.Scheduler.ThreadBufferSize?.Value ?? 500);
            _semaphore = new SemaphoreSlim(0);
            _loggingThread = new Thread(LogWorker)
            {
                Name = threadName
            };
            _loggingThread.Start();
            Wrappers.Add(this);
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
                        
                        if (_taskRingBuffer.TryDequeue(out var task))
                        {
                            task?.Item1.Invoke(task.Item2,task.Item3);
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
                if (ex is ThreadInterruptedException || ex is ThreadAbortException)
                {
                    _shouldRun = () => false;
                    return;
                }
                try
                {
                    AsyncLoggers.Log.LogError($"Bad Exception while logging: {ex}");}
                catch (Exception)
                {
                    Console.WriteLine($"Bad Exception while logging: {ex}");
                }
            }
        }
        
        public void Schedule(IWrapper.LogCallback callback, object sender, BepInEx.Logging.LogEventArgs eventArgs)
        {
            if (_shouldRun != DefaultCondition) 
                return;

            _taskRingBuffer.Enqueue(new (callback, sender, eventArgs));
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
                _shouldRun = () => _taskRingBuffer.Count > 0;
        }
    }
}