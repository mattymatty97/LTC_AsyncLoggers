using System;
using System.Threading;
using BepInEx.Logging;
using Logging = BepInEx.Logging;

namespace AsyncLoggers.Wrappers.EventArgs;

public class LogEventWrapper : Logging.LogEventArgs
{
    private static long _logCounter = 0L;
    
    public DateTime Timestamp { get; }
    
    public string AppTimestamp { get; }

    public long Uuid { get; }

    public string StackTrace { get; internal set; }

    private bool? _isFiltered;
    
    public bool IsFiltered
    {
        get
        {
            _isFiltered ??= CheckFilter();
            return _isFiltered.Value;
        }
    }

    private bool CheckFilter()
    {
        var sourceMask = FilterConfig.GetMaskForSource(Source);
        return (Level & sourceMask) == 0;
    }

    internal LogEventWrapper(LogEventArgs eventArgs) : base(eventArgs.Data, eventArgs.Level, eventArgs.Source)
    {
        Timestamp = DateTime.UtcNow;
        Uuid = Interlocked.Increment(ref _logCounter);
        AppTimestamp = AsyncLoggers.GetLogTimestamp(this).ToString();
    }

    protected LogEventWrapper(object data, LogLevel level, ILogSource source, DateTime timestamp, string appTimestamp, long uuid, string stackTrace) : base(data, level, source)
    {
        Timestamp = timestamp;
        AppTimestamp = appTimestamp;
        Uuid = uuid;
        StackTrace = stackTrace;
    }

    public override string ToString()
    {
        return base.ToString();
    }
}