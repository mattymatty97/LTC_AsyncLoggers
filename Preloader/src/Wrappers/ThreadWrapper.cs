using System;
using System.Collections.Generic;
using System.Threading;
using AsyncLoggers.Buffer;
using AsyncLoggers.Config;
using AsyncLoggers.Wrappers.EventArgs;

namespace AsyncLoggers.Wrappers
{
    public class ThreadWrapper: IWrapper
    {
        internal static readonly HashSet<ThreadWrapper> Wrappers = [];
        private delegate bool RunCondition();

        private readonly Thread _loggingThread;
        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentCircularBuffer<Tuple<IWrapper.LogCallback,object, ExtendedLogEventArgs>> _taskRingBuffer;
        private static readonly RunCondition DefaultCondition = ()=>true;
        private volatile RunCondition _shouldRun = DefaultCondition;

        public event Action OnBecomeIdle;
        public event Action Stopping;

        private static bool _isIdle;

        internal ThreadWrapper(string threadName = nameof(ThreadWrapper))
        {
            _taskRingBuffer = new ConcurrentCircularBuffer<Tuple<IWrapper.LogCallback,object, ExtendedLogEventArgs>>(PluginConfig.Scheduler.ThreadBufferSize?.Value ?? 500);
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
                        {
                            if (!_isIdle)
                                OnBecomeIdle?.Invoke();
                            _isIdle = true;
                            continue;
                        }
                        _isIdle = false;
                        
                        if (_taskRingBuffer.TryDequeue(out var task))
                        {
                            task?.Item1.Invoke(task.Item2,task.Item3);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is ThreadInterruptedException or ThreadAbortException)
                        {
                            _shouldRun = () => false;
                            break;
                        }
                        try
                        {
                            AsyncLoggers.EmergencyLog.LogError($"Exception while logging: {ex}");
                        }
                        catch (Exception)
                        {
                            Console.WriteLine($"Exception while logging: {ex}");
                        }
                        
                        if (_shouldRun != DefaultCondition) 
                            break;
                    }
                }

                Stopping?.Invoke();
            }
            catch (Exception ex)
            {
                if (ex is ThreadInterruptedException || ex is ThreadAbortException)
                {
                    _shouldRun = () => false;
                    Stopping?.Invoke();
                    return;
                }
                
                try
                {
                    AsyncLoggers.EmergencyLog.LogError($"Bad Exception while logging: {ex}");}
                catch (Exception)
                {
                    Console.WriteLine($"Bad Exception while logging: {ex}");
                }
            }
        }
        
        public void Schedule(IWrapper.LogCallback callback, object sender, ExtendedLogEventArgs eventArgs)
        {
            if (_shouldRun != DefaultCondition) 
                return;

            _taskRingBuffer.Enqueue(new Tuple<IWrapper.LogCallback, object, ExtendedLogEventArgs>(callback, sender, eventArgs));
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
