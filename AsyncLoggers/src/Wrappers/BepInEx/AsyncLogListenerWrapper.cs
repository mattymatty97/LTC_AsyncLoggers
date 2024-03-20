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
            if (AsyncLoggers.PluginConfig.BepInEx.Scheduler.Value == AsyncLoggers.PluginConfig.AsyncType.Thread)
                _threadWrapper = new ThreadWrapper();
            else
            {
                _threadWrapper = new JobWrapper();
            }
            _baseListener = baseListener;
        }

        public void Dispose()
        {
            var instant = AsyncLoggers.PluginConfig.Scheduler.ShutdownType.Value ==
                          AsyncLoggers.PluginConfig.ShutdownType.Instant;
            if (instant)
                _baseListener.Dispose();
            else
                _threadWrapper.Schedule(()=>_baseListener.Dispose());
            _threadWrapper.Stop(instant);
        }

        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            _threadWrapper.Schedule(()=>_baseListener.LogEvent(sender,eventArgs));
        }
    }
}