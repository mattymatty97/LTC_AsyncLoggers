using System;
using System.Collections.Concurrent;
using System.Threading;

namespace AsyncLoggers.Cecil;

public static class ThrothledLogWrapper
{
    internal static long CurrID => _currID;
    internal static long NextID => Interlocked.Increment(ref _currID);

    private static readonly ConcurrentDictionary<long, DateTime> TimestampMemory = [];
    internal static readonly ConcurrentDictionary<long, TimeSpan> CooldownMemory = [];
    private static long _currID;

    private static bool InCooldown(long key)
    {
        if (TimestampMemory.TryGetValue(key, out var timestamp))
        {
            if (DateTime.UtcNow - timestamp < CooldownMemory[key])
            {
                return true;
            }
        }
        
        TimestampMemory[key] = DateTime.UtcNow;
        
        return false;
    }
    
    
    // Method for logs without context
    public static void LogInfo(long logKey, string message)
    {
        if(InCooldown(logKey))
            return;
        
        AsyncLoggers.WrappedUnitySource.LogInfo(message);
    }

    public static void LogError(long logKey, string message)
    {
        if (InCooldown(logKey)) 
            return;
        
        AsyncLoggers.WrappedUnitySource.LogError(message);
    }

    public static void LogWarning(long logKey, string message)
    {
        if(InCooldown(logKey))
            return;
        
        AsyncLoggers.WrappedUnitySource.LogWarning(message);
    }
    
    public static void LogInfoFormat(long logKey, string format, params object[] args)
    {
        var message = string.Format(format, args);
        LogInfo(logKey, message);
    }

    public static void LogErrorFormat(long logKey, string format, params object[] args)
    {
        var message = string.Format(format, args);
        LogError(logKey, message);
    }

    public static void LogWarningFormat(long logKey, string format, params object[] args)
    {
        var message = string.Format(format, args);
        LogWarning(logKey, message);
    }

    // Method for logs with context
    public static void LogInfoWithContext(long logKey, string message, object context)
    {
        var formattedMessage = FormatMessageWithContext(message, context);
        LogInfo(logKey, formattedMessage);
    }

    public static void LogErrorWithContext(long logKey, string message, object context)
    {
        var formattedMessage = FormatMessageWithContext(message, context);
        LogError(logKey, formattedMessage);
    }

    public static void LogWarningWithContext(long logKey, string message, object context)
    {
        var formattedMessage = FormatMessageWithContext(message, context);
        LogWarning(logKey, formattedMessage);
    }

    public static void LogInfoFormatWithContext(long logKey, object context, string format, params object[] args)
    {
        var message = string.Format(format, args);
        LogInfoWithContext(logKey, message, context);
    }

    public static void LogErrorFormatWithContext(long logKey, object context, string format, params object[] args)
    {
        var message = string.Format(format, args);
        LogErrorWithContext(logKey, message, context);
    }

    public static void LogWarningFormatWithContext(long logKey, object context, string format, params object[] args)
    {
        var message = string.Format(format, args);
        LogWarningWithContext(logKey, message, context);
    }

    // Helper method for formatting messages
    private static string FormatMessageWithContext(string message, object context)
    {
        return $"{message} (Context: {context})";
    }
}