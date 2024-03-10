using System;
using BepInEx.Logging;

namespace AsyncLoggers.Utilities
{
    public class AsyncLogListenerWrapper: ILogListener
    {
        
        private readonly AsyncWrapper _asyncWrapper;
        private readonly ILogListener _baseListener;

        public AsyncLogListenerWrapper(ILogListener baseListener)
        {
            if (baseListener is AsyncLogListenerWrapper)
                throw new ArgumentException("Cannot nest AsyncLoggers");
            _asyncWrapper = new AsyncWrapper();
            _baseListener = baseListener;
        }

        public void Dispose()
        {
            _asyncWrapper.Stop();
        }

        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            _asyncWrapper.Schedule(()=>_baseListener.LogEvent(sender,eventArgs));
        }
    }
}