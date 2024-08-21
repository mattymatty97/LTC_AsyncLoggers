using System.Runtime.CompilerServices;
using BepInEx.Logging;

namespace AsyncLoggers.Wrappers.EventArgs;

public static class WrapperExtensions
{
    private static readonly ConditionalWeakTable<LogEventArgs, LogEventWrapper> WrapperMap = [];
    private static readonly ConditionalWeakTable<LogEventWrapper, TimestampedLogEventArg> TimestampMap = [];

    public static LogEventWrapper AsLogEventWrapper(this LogEventArgs @this)
    {
        if (@this is LogEventWrapper wrapper)
            return wrapper;
        
        if (WrapperMap.TryGetValue(@this, out wrapper))
            return wrapper;

        wrapper = new LogEventWrapper(@this);

        WrapperMap.AddOrUpdate(@this, wrapper);

        return wrapper;
    }
    
    public static TimestampedLogEventArg AsTimestampedLogEventArg(this LogEventWrapper @this)
    {
        if (@this is TimestampedLogEventArg wrapper)
            return wrapper;
        
        if (TimestampMap.TryGetValue(@this, out wrapper))
            return wrapper;

        wrapper = new TimestampedLogEventArg(@this);

        TimestampMap.AddOrUpdate(@this, wrapper);

        return wrapper;
    }
}