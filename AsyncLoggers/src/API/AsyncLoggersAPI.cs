using System;
using System.Collections.Generic;
using AsyncLoggers.Patches;
using BepInEx.Logging;
using static AsyncLoggers.API.AsyncLoggersAPI.LogListenerFlags;

namespace AsyncLoggers.API;

public static class AsyncLoggersAPI
{
    /// <summary>
    ///     Flags that control the behavior of log listeners.
    /// </summary>
    [Flags]
    public enum LogListenerFlags
    {
        /// <summary>
        ///     No special behavior. Default value.
        /// </summary>
        None = 0,

        /// <summary>
        ///     This listener will receive logs directly, without any buffering or delay.
        /// </summary>
        SyncHandling = 1 << 1,

        /// <summary>
        ///     This listener will receive all logs, bypassing any filters that might be applied.
        /// </summary>
        IgnoreFilters = 1 << 2,

        /// <summary>
        ///     This listener will have a timestamp prepended to the log messages.
        /// </summary>
        AddTimeStamp = 1 << 3
    }

    /// <summary>
    ///     Gets the version of the API.
    /// </summary>
    /// <value>
    ///     The current version of the API, represented as a <see cref="Version" />.
    /// </value>
    public static Version APIVersion { get; } = new(1, 0, 0, 0);

    /// <summary>
    ///     Gets or sets the log levels that will collect and store a stack trace.
    /// </summary>
    /// <value>
    ///     A bitwise combination of <see cref="LogLevel" /> values that determines which log levels
    ///     will have a stack trace collected and stored. By default, this includes <see cref="LogLevel.Error" />
    ///     and <see cref="LogLevel.Fatal" />.
    /// </value>
    public static LogLevel TraceableLevelsMaks { get; set; } = LogLevel.Error | LogLevel.Fatal;

    /// <summary>
    ///     Updates the flags associated with a log listener.
    /// </summary>
    /// <param name="target">The log listener to update.</param>
    /// <param name="flags">A combination of <see cref="LogListenerFlags" /> that specify the listener's behavior.</param>
    /// <remarks>
    ///     This method is used to control how a listener receives logs,
    ///     such as whether it receives logs directly (without buffering),
    ///     ignores user filters, or prepends timestamps.
    /// </remarks>
    public static void UpdateListenerFlags(ILogListener target, LogListenerFlags flags)
    {
        UpdateListener(target, flags, SyncHandling, LoggerPatch.SyncListeners);
        UpdateListener(target, flags, IgnoreFilters, LoggerPatch.UnfilteredListeners);
        UpdateListener(target, flags, AddTimeStamp, LoggerPatch.TimestampedListeners);
    }

    private static void UpdateListener(ILogListener target, LogListenerFlags flags, LogListenerFlags flagToCheck,
        ICollection<ILogListener> listenerCollection)
    {
        if ((flags & flagToCheck) != 0)
            listenerCollection.Add(target);
        else
            listenerCollection.Remove(target);
    }
}