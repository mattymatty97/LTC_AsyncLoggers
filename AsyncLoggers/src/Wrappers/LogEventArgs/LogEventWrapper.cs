using System;
using System.Threading;
using BepInEx.Logging;
using Logging = BepInEx.Logging;

namespace AsyncLoggers.Wrappers.LogEventArgs;

public class LogEventWrapper : Logging.LogEventArgs
{
    private static long _logCounter = 0L;
    
    public readonly DateTime Timestamp;

    public readonly long Uuid;

    public readonly string StackTrace;

    public LogEventWrapper(object data, LogLevel level, ILogSource source, string stackTrace = null) : base(data, level, source)
    {
        StackTrace = stackTrace;
        Timestamp = DateTime.UtcNow;
        Uuid = Interlocked.Increment(ref _logCounter);
    }

    public string FormatTimestamp()
    {
        return Timestamp.ToString("MM/dd/yyyy HH:mm:ss.fffffff");
    }
}