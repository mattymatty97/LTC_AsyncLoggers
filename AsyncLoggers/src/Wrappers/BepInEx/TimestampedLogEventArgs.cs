using System;
using BepInEx.Logging;

namespace AsyncLoggers.Wrappers.BepInEx
{
    public class TimestampedLogEventArgs: LogEventArgs
    {
        private readonly LogEventArgs _baseArgs;
        
        public TimestampedLogEventArgs(LogEventArgs baseArgs) : base(baseArgs.Data, baseArgs.Level, baseArgs.Source)
        {
            _baseArgs = baseArgs;
        }

        public override string ToString()
        {
            object timestamp = AsyncLoggerPreloader.GetCurrTimestamp();
            return $"[{timestamp}] {_baseArgs}";
        }
    }
}