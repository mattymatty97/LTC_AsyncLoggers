using System;
using BepInEx.Logging;

namespace AsyncLoggers.Wrappers.BepInEx
{
    public class AsyncLogListenerWrapper: ILogListener
    {
        
        private readonly IAsyncWrapper _threadWrapper;
        private readonly ILogListener _baseListener;

        public AsyncLogListenerWrapper(ILogListener baseListener)
        {
            if (baseListener is AsyncLogListenerWrapper)
                throw new ArgumentException("Cannot nest AsyncLoggers");
            if (PluginConfig.BepInEx.Scheduler.Value == PluginConfig.AsyncType.Thread)
                _threadWrapper = new ThreadWrapper();
            else
            {
                _threadWrapper = new JobWrapper();
            }
            _baseListener = baseListener;
        }

        public void Dispose()
        {
            var instant = PluginConfig.Scheduler.ShutdownType.Value ==
                          PluginConfig.ShutdownType.Instant;
            if (instant)
                _baseListener.Dispose();
            else
                _threadWrapper.Schedule(()=>_baseListener.Dispose());
            _threadWrapper.Stop(instant);
        }

        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            object timestamp = AsyncLoggerPreloader.GetCurrTimestamp();
            _threadWrapper.Schedule(()=>
            {
                AsyncLoggerPreloader.logTimestamp.Value = timestamp;
                _baseListener.LogEvent(sender, PluginConfig.Timestamps.Enabled.Value ? new TimestampedLogEventArgs(eventArgs) : eventArgs);
                AsyncLoggerPreloader.logTimestamp.Value = null;
            });
        }
    }
}