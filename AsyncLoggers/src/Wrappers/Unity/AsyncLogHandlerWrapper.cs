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
            _asyncWrapper.Schedule(()=>_baseHandler.LogFormat(logType,context,format,args));
        }
        
        public void LogException(Exception exception, Object context)
        {
            _asyncWrapper.Schedule(()=>_baseHandler.LogException(exception,context));
        }

    }
}