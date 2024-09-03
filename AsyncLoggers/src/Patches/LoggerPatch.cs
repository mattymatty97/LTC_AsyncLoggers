using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AsyncLoggers.API;
using AsyncLoggers.Config;
using AsyncLoggers.Wrappers;
using AsyncLoggers.Wrappers.EventArgs;
using BepInEx.Bootstrap;
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


    internal static readonly ConcurrentDictionary<ILogListener, IWrapper> WrappersMap = [];

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Logger), nameof(Logger.InternalLogEvent))]
    private static bool WrapLogs(object sender, LogEventArgs eventArgs)
    {
        if (!Chainloader._initialized)
            return true;
        
        if (sender == AsyncLoggers.Log)
            return true;
        
        var wrappedEvent = eventArgs.AsLogEventWrapper();

        if ((eventArgs.Level & AsyncLoggersAPI.TraceableLevelsMask) != 0 && sender is not ManualLogSource { SourceName: "Preloader" })
        {
            wrappedEvent.StackTrace = Environment.StackTrace;
        }
        
        var timestampedEvent = PluginConfig.Timestamps.Enabled.Value ? wrappedEvent.AsTimestampedLogEventArg() : wrappedEvent;

        foreach (var listener in SyncListeners)
        {
            if (ShouldEmitLog(listener, wrappedEvent))
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
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldEmitLog(ILogListener listener, LogEventWrapper eventArgs)
    {
        return UnfilteredListeners.Contains(listener) || !eventArgs.IsFiltered;
    }

    private static void HandleLogEvent(object sender, LogEventArgs eventArgs)
    {
        var wrappedEventArgs = eventArgs.AsLogEventWrapper();
        var timestampedEvent = PluginConfig.Timestamps.Enabled.Value ? wrappedEventArgs.AsTimestampedLogEventArg() : wrappedEventArgs;
        try
        {
            var list = new LinkedList<ILogListener>(Logger.Listeners.Where(l => !SyncListeners.Contains(l)));
            foreach (var listener in list)
            {
                if (!ShouldEmitLog(listener, wrappedEventArgs)) 
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