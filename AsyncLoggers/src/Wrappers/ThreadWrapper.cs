﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using DisruptorUnity3d;
using UnityEngine;

namespace AsyncLoggers.Wrappers
{
    public class ThreadWrapper: IWrapper
    {
        internal static readonly HashSet<ThreadWrapper> _wrappers = new HashSet<ThreadWrapper>();
        private delegate bool RunCondition();

        private readonly Thread _loggingThread;
        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentCircularBuffer<IWrapper.LogCallback> _taskRingBuffer;
        private static readonly RunCondition DefaultCondition = ()=>true;
        private volatile RunCondition _shouldRun = DefaultCondition;

        internal ThreadWrapper()
        {
            _taskRingBuffer = new ConcurrentCircularBuffer<IWrapper.LogCallback>(PluginConfig.Scheduler.ThreadBufferSize.Value);
            _semaphore = new SemaphoreSlim(0);
            _loggingThread = new Thread(LogWorker);
            _loggingThread.Start();
            _wrappers.Add(this);
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
                        if (!_semaphore.Wait(1000))
                            continue;
                        
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
        
        public void Schedule(IWrapper.LogCallback callback)
        {
            if (_shouldRun != DefaultCondition) 
                return;
            
            var logUUID = LogContext.Uuid;
            var timestamp = LogContext.Timestamp;

            _taskRingBuffer.Enqueue(CallbackWrapper);
            _semaphore.Release();
            return;

            void CallbackWrapper()
            {
                try
                {
                    LogContext.Async = true;
                    LogContext.Timestamp = timestamp;
                    LogContext.Uuid = logUUID;
                    callback();
                }
                finally
                {
                    LogContext.Async = false;
                    LogContext.Timestamp = null;
                    LogContext.Uuid = null;
                }
            }
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