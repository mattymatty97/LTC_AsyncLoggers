namespace AsyncLoggers.Cecil;

public static class LogWrapper
{
    // Method for logs without context
    public static void LogInfo(object message)
    {
        AsyncLoggers.WrappedUnitySource.LogInfo(message);
    }

    public static void LogError(object message)
    {
        AsyncLoggers.WrappedUnitySource.LogError(message);
    }

    public static void LogWarning(object message)
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
    public static void LogInfoWithContext(object message, object context)
    {
        var formattedMessage = FormatMessageWithContext(message, context);
        LogInfo(formattedMessage);
    }

    public static void LogErrorWithContext(object message, object context)
    {
        var formattedMessage = FormatMessageWithContext(message, context);
        LogError(formattedMessage);
    }

    public static void LogWarningWithContext(object message, object context)
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
    private static string FormatMessageWithContext(object message, object context)
    {
        return $"{message} (Context: {context})";
    }
}