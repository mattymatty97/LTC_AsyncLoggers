using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AsyncLoggers.Wrappers.Unity
{
    public class AsyncLoggerWrapper : ILogger
    {
        private readonly IAsyncWrapper _asyncWrapper;
        private readonly ILogger _baseLogger;

        public AsyncLoggerWrapper(ILogger baseLogger)
        {
            if (baseLogger is AsyncLoggerWrapper)
                throw new ArgumentException("Cannot nest AsyncLoggers");
            if (PluginConfig.Unity.Scheduler.Value == PluginConfig.AsyncType.Thread)
                _asyncWrapper = new ThreadWrapper();
            else
            {
                _asyncWrapper = new JobWrapper();
            }
            _baseLogger = baseLogger;
        }
        
        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            object currTimestamp = AsyncLoggerPreloader.GetCurrTimestamp();
            _asyncWrapper.Schedule(()=>
            {
                AsyncLoggerPreloader.logTimestamp.Value = currTimestamp;
                _baseLogger.LogFormat(logType, context, format, args);
                AsyncLoggerPreloader.logTimestamp.Value = null;
            });
        }

        public void LogException(Exception exception, Object context)
        {
            object currTimestamp = AsyncLoggerPreloader.GetCurrTimestamp();
            _asyncWrapper.Schedule(()=>
            {
                AsyncLoggerPreloader.logTimestamp.Value = currTimestamp;
                _baseLogger.LogException(exception, context);
                AsyncLoggerPreloader.logTimestamp.Value = null;
            });
        }

        public bool IsLogTypeAllowed(LogType logType)
        {
            return true;
        }

        public void Log(LogType logType, object message)
        {
            object currTimestamp = AsyncLoggerPreloader.GetCurrTimestamp();
            _asyncWrapper.Schedule(()=>
            {
                AsyncLoggerPreloader.logTimestamp.Value = currTimestamp;
                _baseLogger.Log(logType, message);
                AsyncLoggerPreloader.logTimestamp.Value = null;
            });
        }

        public void Log(LogType logType, object message, Object context)
        {
            object currTimestamp = AsyncLoggerPreloader.GetCurrTimestamp();
            _asyncWrapper.Schedule(()=>
            {
                AsyncLoggerPreloader.logTimestamp.Value = currTimestamp;
                _baseLogger.Log(logType, message, context);
                AsyncLoggerPreloader.logTimestamp.Value = null;
            });
        }

        public void Log(LogType logType, string tag, object message)
        {
            object currTimestamp = AsyncLoggerPreloader.GetCurrTimestamp();
            _asyncWrapper.Schedule(()=>
            {
                AsyncLoggerPreloader.logTimestamp.Value = currTimestamp;
                _baseLogger.Log(logType, tag, message);
                AsyncLoggerPreloader.logTimestamp.Value = null;
            });
        }

        public void Log(LogType logType, string tag, object message, Object context)
        {
            object currTimestamp = AsyncLoggerPreloader.GetCurrTimestamp();
            _asyncWrapper.Schedule(()=>
            {
                AsyncLoggerPreloader.logTimestamp.Value = currTimestamp;
                _baseLogger.Log(logType, tag, message, context);
                AsyncLoggerPreloader.logTimestamp.Value = null;
            });
        }

        public void Log(object message)
        {
            object currTimestamp = AsyncLoggerPreloader.GetCurrTimestamp();
            _asyncWrapper.Schedule(()=>
            {
                AsyncLoggerPreloader.logTimestamp.Value = currTimestamp;
                _baseLogger.Log(message);
                AsyncLoggerPreloader.logTimestamp.Value = null;
            });
        }

        public void Log(string tag, object message)
        {
            object currTimestamp = AsyncLoggerPreloader.GetCurrTimestamp();
            _asyncWrapper.Schedule(()=>
            {
                AsyncLoggerPreloader.logTimestamp.Value = currTimestamp;
                _baseLogger.Log(tag, message);
                AsyncLoggerPreloader.logTimestamp.Value = null;
            });
        }

        public void Log(string tag, object message, Object context)
        {
            object currTimestamp = AsyncLoggerPreloader.GetCurrTimestamp();
            _asyncWrapper.Schedule(()=>
            {
                AsyncLoggerPreloader.logTimestamp.Value = currTimestamp;
                _baseLogger.Log(tag, message, context);
                AsyncLoggerPreloader.logTimestamp.Value = null;
            });
        }

        public void LogWarning(string tag, object message)
        {
            object currTimestamp = AsyncLoggerPreloader.GetCurrTimestamp();
            _asyncWrapper.Schedule(()=>
            {
                AsyncLoggerPreloader.logTimestamp.Value = currTimestamp;
                _baseLogger.LogWarning(tag, message);
                AsyncLoggerPreloader.logTimestamp.Value = null;
            });
        }

        public void LogWarning(string tag, object message, Object context)
        {
            object currTimestamp = AsyncLoggerPreloader.GetCurrTimestamp();
            _asyncWrapper.Schedule(()=>
            {
                AsyncLoggerPreloader.logTimestamp.Value = currTimestamp;
                _baseLogger.LogWarning(tag, message, context);
                AsyncLoggerPreloader.logTimestamp.Value = null;
            });
        }

        public void LogError(string tag, object message)
        {
            object currTimestamp = AsyncLoggerPreloader.GetCurrTimestamp();
            _asyncWrapper.Schedule(()=>
            {
                AsyncLoggerPreloader.logTimestamp.Value = currTimestamp;
                _baseLogger.LogError(tag, message);
                AsyncLoggerPreloader.logTimestamp.Value = null;
            });
        }

        public void LogError(string tag, object message, Object context)
        {
            object currTimestamp = AsyncLoggerPreloader.GetCurrTimestamp();
            _asyncWrapper.Schedule(()=>
            {
                AsyncLoggerPreloader.logTimestamp.Value = currTimestamp;
                _baseLogger.LogError(tag, message, context);
                AsyncLoggerPreloader.logTimestamp.Value = null;
            });
        }

        public void LogFormat(LogType logType, string format, params object[] args)
        {
            object currTimestamp = AsyncLoggerPreloader.GetCurrTimestamp();
            _asyncWrapper.Schedule(()=>
            {
                AsyncLoggerPreloader.logTimestamp.Value = currTimestamp;
                _baseLogger.LogFormat(logType, format, args);
                AsyncLoggerPreloader.logTimestamp.Value = null;
            });
        }

        public void LogException(Exception exception)
        {
            object currTimestamp = AsyncLoggerPreloader.GetCurrTimestamp();
            _asyncWrapper.Schedule(() =>
            {
                AsyncLoggerPreloader.logTimestamp.Value = currTimestamp;
                _baseLogger.LogException(exception);
                AsyncLoggerPreloader.logTimestamp.Value = null;
            });
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