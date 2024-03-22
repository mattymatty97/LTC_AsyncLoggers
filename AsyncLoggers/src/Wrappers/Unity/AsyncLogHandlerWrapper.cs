using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AsyncLoggers.Wrappers.Unity
{
    public class AsyncLogHandlerWrapper: ILogHandler
    {
        private readonly IAsyncWrapper _asyncWrapper;
        private readonly ILogHandler _baseHandler;

        public AsyncLogHandlerWrapper(ILogHandler baseHandler)
        {
            if (baseHandler is AsyncLogHandlerWrapper)
                throw new ArgumentException("Cannot nest AsyncLoggers");
            if (PluginConfig.Unity.Scheduler.Value == PluginConfig.AsyncType.Thread)
                _asyncWrapper = new ThreadWrapper();
            else
            {
                _asyncWrapper = new JobWrapper();
            }
            _baseHandler = baseHandler;
        }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            object timestamp = AsyncLoggerPreloader.GetCurrTimestamp();
            _asyncWrapper.Schedule(()=>
            {
                AsyncLoggerPreloader.logTimestamp.Value = timestamp;
                _baseHandler.LogFormat(logType, context, format, args);
                AsyncLoggerPreloader.logTimestamp.Value = null;
            });
        }
        
        public void LogException(Exception exception, Object context)
        {
            
            object timestamp = AsyncLoggerPreloader.GetCurrTimestamp();
            _asyncWrapper.Schedule(() =>
            {
                AsyncLoggerPreloader.logTimestamp.Value = timestamp;
                _baseHandler.LogException(exception, context);
                AsyncLoggerPreloader.logTimestamp.Value = null;
            });
        }

    }
}