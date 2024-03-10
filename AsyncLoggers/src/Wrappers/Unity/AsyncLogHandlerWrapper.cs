using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AsyncLoggers.Utilities
{
    public class AsyncLogHandlerWrapper: ILogHandler
    {
        private readonly AsyncWrapper _asyncWrapper;
        private readonly ILogHandler _baseHandler;

        public AsyncLogHandlerWrapper(ILogHandler baseHandler)
        {
            if (baseHandler is AsyncLogHandlerWrapper)
                throw new ArgumentException("Cannot nest AsyncLoggers");
            _asyncWrapper = new AsyncWrapper();
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