using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AsyncLoggers.Wrappers.Unity
{
    public class AsyncLoggerWrapper : ILogger
    {
        private readonly IWrapper _wrapper;
        private readonly ILogger _baseLogger;

        public AsyncLoggerWrapper(ILogger baseLogger)
        {
            if (baseLogger is AsyncLoggerWrapper)
                throw new ArgumentException("Cannot nest AsyncLoggers");
            if (PluginConfig.Unity.Scheduler.Value == PluginConfig.AsyncType.Thread)
                _wrapper = new ThreadWrapper();
            else
            {
                _wrapper = new JobWrapper();
            }
            _baseLogger = baseLogger;
        }
        
        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            _wrapper.Schedule(()=> _baseLogger.LogFormat(logType, context, format, args));
        }

        public void LogException(Exception exception, Object context)
        {
            _wrapper.Schedule(()=>_baseLogger.LogException(exception, context));
        }

        public bool IsLogTypeAllowed(LogType logType)
        {
            return true;
        }

        public void Log(LogType logType, object message)
        {
            _wrapper.Schedule(()=>_baseLogger.Log(logType, message));
        }

        public void Log(LogType logType, object message, Object context)
        {
            _wrapper.Schedule(()=>_baseLogger.Log(logType, message, context));
        }

        public void Log(LogType logType, string tag, object message)
        {
            _wrapper.Schedule(()=>_baseLogger.Log(logType, tag, message));
        }

        public void Log(LogType logType, string tag, object message, Object context)
        {
            _wrapper.Schedule(()=>_baseLogger.Log(logType, tag, message, context));
        }

        public void Log(object message)
        {
            _wrapper.Schedule(()=>_baseLogger.Log(message));
        }

        public void Log(string tag, object message)
        {
            _wrapper.Schedule(()=>_baseLogger.Log(tag, message));
        }

        public void Log(string tag, object message, Object context)
        {
            _wrapper.Schedule(()=>_baseLogger.Log(tag, message, context));
        }

        public void LogWarning(string tag, object message)
        {
            _wrapper.Schedule(()=>_baseLogger.LogWarning(tag, message));
        }

        public void LogWarning(string tag, object message, Object context)
        {
            _wrapper.Schedule(()=>_baseLogger.LogWarning(tag, message, context));
        }

        public void LogError(string tag, object message)
        {
            _wrapper.Schedule(()=>_baseLogger.LogError(tag, message));
        }

        public void LogError(string tag, object message, Object context)
        {
            _wrapper.Schedule(()=>_baseLogger.LogError(tag, message, context));
        }

        public void LogFormat(LogType logType, string format, params object[] args)
        {
            _wrapper.Schedule(()=>_baseLogger.LogFormat(logType, format, args));
        }

        public void LogException(Exception exception)
        {
            _wrapper.Schedule(() =>_baseLogger.LogException(exception));
        }

        public ILogHandler logHandler
        {
            get => _baseLogger.logHandler;
            set => _baseLogger.logHandler = value;
        }
        public bool logEnabled { 
            get => _baseLogger.logEnabled;
            set => _baseLogger.logEnabled = value; 
        }

        public LogType filterLogType
        {
            get => _baseLogger.filterLogType; 
            set => _baseLogger.filterLogType = value;
        }
    }
}