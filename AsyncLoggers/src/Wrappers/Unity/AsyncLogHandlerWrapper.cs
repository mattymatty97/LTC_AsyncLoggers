using System;
using AsyncLoggers.StaticContexts;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AsyncLoggers.Wrappers.Unity
{
    public class AsyncLogHandlerWrapper: ILogHandler
    {
        private readonly IWrapper _wrapper;
        private readonly ILogHandler _baseHandler;

        public AsyncLogHandlerWrapper(ILogHandler baseHandler)
        {
            if (baseHandler is AsyncLogHandlerWrapper)
                throw new ArgumentException("Cannot nest AsyncLoggers");
            if (PluginConfig.Unity.Scheduler.Value == PluginConfig.AsyncType.Thread)
                _wrapper = new ThreadWrapper();
            else
            {
                _wrapper = new JobWrapper();
            }
            _baseHandler = baseHandler;
        }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            _wrapper.Schedule(wrapCallback(() => _baseHandler.LogFormat(logType, context, format, args)));
        }
        
        public void LogException(Exception exception, Object context)
        {
            _wrapper.Schedule(wrapCallback(() => _baseHandler.LogException(exception, context)));
        }

        private static IWrapper.LogCallback wrapCallback(Action callback)
        {
            var timestamp = GenericContext.Timestamp;
            var uuid = LogContext.Uuid;
            return () =>
            {
                try
                {
                    GenericContext.Async = true;
                    GenericContext.Timestamp = timestamp;
                    LogContext.Uuid = uuid;
                    callback();
                }
                catch (Exception ex)
                {
                    AsyncLoggers.Log.LogError(
                        $"Exception dispatching log to Unity: {ex}");
                }
                finally
                {
                    GenericContext.Async = false;
                    GenericContext.Timestamp = null;
                    LogContext.Uuid = null;
                }
            };
        }
    }
}