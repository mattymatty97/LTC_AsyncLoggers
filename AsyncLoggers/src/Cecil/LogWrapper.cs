using System;
using BepInEx.Logging;

namespace AsyncLoggers.Cecil;

public static class LogWrapper
{
    // Method for logs without context
    public static void LogInfo(string message)
    {
        AsyncLoggers.WrappedUnitySource.LogInfo(message);
    }

    public static void LogError(string message)
    {
        AsyncLoggers.WrappedUnitySource.LogError(message);
    }

    public static void LogWarning(string message)
    {
        AsyncLoggers.WrappedUnitySource.LogWarning(message);
    }
    
    public static void LogInfoFormat(string format, params object[] args)
    {
        var message = string.Format(format, args);
        LogInfo(message);
    }

    public static void LogErrorFormat(string format, params object[] args)
    {
        var message = string.Format(format, args);
        LogError(message);
    }

    public static void LogWarningFormat(string format, params object[] args)
    {
        var message = string.Format(format, args);
        LogWarning(message);
    }

    // Method for logs with context
    public static void LogInfoWithContext(string message, object context)
    {
        var formattedMessage = FormatMessageWithContext(message, context);
        LogInfo(formattedMessage);
    }

    public static void LogErrorWithContext(string message, object context)
    {
        var formattedMessage = FormatMessageWithContext(message, context);
        LogError(formattedMessage);
    }

    public static void LogWarningWithContext(string message, object context)
    {
        var formattedMessage = FormatMessageWithContext(message, context);
        LogWarning(formattedMessage);
    }

    public static void LogInfoFormatWithContext(object context, string format, params object[] args)
    {
        var message = string.Format(format, args);
        LogInfoWithContext(message, context);
    }

    public static void LogErrorFormatWithContext(object context, string format, params object[] args)
    {
        var message = string.Format(format, args);
        LogErrorWithContext(message, context);
    }

    public static void LogWarningFormatWithContext(object context, string format, params object[] args)
    {
        var message = string.Format(format, args);
        LogWarningWithContext(message, context);
    }

    // Helper method for formatting messages
    private static string FormatMessageWithContext(string message, object context)
    {
        return $"{message} (Context: {context})";
    }
}