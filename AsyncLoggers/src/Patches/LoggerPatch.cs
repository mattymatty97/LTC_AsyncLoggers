using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using AsyncLoggers.Wrappers;
using AsyncLoggers.Wrappers.LogEventArgs;
using BepInEx.Logging;
using HarmonyLib;
using Logger = BepInEx.Logging.Logger;

namespace AsyncLoggers.Patches;

[HarmonyPatch]
internal class LoggerPatch
{
    private static readonly IWrapper MainWrapper = new ThreadWrapper("Log Dispatcher");

    internal static readonly HashSet<ILogListener> SyncListeners = [];
    internal static readonly HashSet<ILogListener> UnfilteredListeners = [];
    internal static readonly HashSet<ILogListener> TimestampedListeners = [];


    private static readonly ConcurrentDictionary<ILogListener, IWrapper> WrappersMap = [];

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Logger), nameof(Logger.InternalLogEvent))]
    private static bool WrapLogs(object sender, LogEventArgs eventArgs)
    {
        if (!PluginConfig.BepInEx.Enabled.Value)
            return true;

        if (sender == AsyncLoggers.Log)
            return true;
        
        string stacktrace = null;
        if ((eventArgs.Level & AsyncLoggers.TraceableLevelsMaks) != 0)
        {
            stacktrace = Environment.StackTrace;
        }
        
        var wrappedEvent = new LogEventWrapper(eventArgs.Data, eventArgs.Level, eventArgs.Source, stacktrace);
        var timestampedEvent = PluginConfig.Timestamps.Enabled.Value ?
        new TimestampedLogEventArg(wrappedEvent) : wrappedEvent;

        foreach (var listener in SyncListeners)
        {
            if (ShouldEmitLog(listener, wrappedEvent.Source, wrappedEvent.Level))
            {
                listener.LogEvent(sender, TimestampedListeners.Contains(listener) ? timestampedEvent : wrappedEvent);
            }
        }

        if (Logger.Listeners.Any(l => !SyncListeners.Contains(l)))
        {
            MainWrapper.Schedule(HandleLogEvent, sender, wrappedEvent);
        }

        return false;
    }

    private static bool ShouldEmitLog(ILogListener listener, ILogSource source, LogLevel level)
    {
        if (UnfilteredListeners.Contains(listener))
            return true;

        var sourceMask = FilterConfig.GetMaskForSource(source);
        return (level & sourceMask) != 0;
    }

    private static void HandleLogEvent(object sender, LogEventArgs eventArgs)
    {
        var timestampedEvent = PluginConfig.Timestamps.Enabled.Value ? new TimestampedLogEventArg((LogEventWrapper)eventArgs) : eventArgs;
        try
        {
            var list = new LinkedList<ILogListener>(Logger.Listeners.Where(l => !SyncListeners.Contains(l)));
            foreach (var listener in list)
            {
                if (!ShouldEmitLog(listener, eventArgs.Source, eventArgs.Level)) 
                    continue;

                var wrapper = WrappersMap.GetOrAdd(listener, l => new ThreadWrapper($"{l.GetType().Name} Wrapper"));

                wrapper?.Schedule(DispatchLogEvent, sender, TimestampedListeners.Contains(listener) ? timestampedEvent : eventArgs);
                
                continue;

                void DispatchLogEvent(object iSender, LogEventArgs iEventArgs)
                {
                    try
                    {
                        listener.LogEvent(iSender, iEventArgs);
                    }
                    catch (Exception ex)
                    {
                        AsyncLoggers.Log.LogError($"Exception dispatching log to {listener!.GetType().Name}: {ex}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AsyncLoggers.Log.LogError(
                $"Exception dispatching log: {ex}");
        }
    }
}