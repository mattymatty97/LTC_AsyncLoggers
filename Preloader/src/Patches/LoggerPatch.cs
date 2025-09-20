using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AsyncLoggers.API;
using AsyncLoggers.Wrappers;
using AsyncLoggers.Wrappers.EventArgs;
using BepInEx.Logging;
using JetBrains.Annotations;
using Logger = BepInEx.Logging.Logger;

namespace AsyncLoggers.Patches;

internal class LoggerPatch
{
    private static readonly IWrapper MainWrapper = new ThreadWrapper("Log Dispatcher");

    internal static readonly HashSet<ILogListener> SyncListeners = [];
    internal static readonly HashSet<ILogListener> UnfilteredListeners = [];
    internal static readonly HashSet<ILogListener> TimestampedListeners = [];

    internal static readonly ConcurrentDictionary<ILogListener, IWrapper> WrappersMap = [];

    private static void NewLogEventProcessor(object sender, LogEventArgs eventArgs)
    {

        if (sender == AsyncLoggers.EmergencyLog)
        {
            Logger.InternalLogEvent(sender, eventArgs);
            return;
        }

        string stackTrace = null;

        if ((eventArgs.Level & AsyncLoggersAPI.TraceableLevelsMask) != 0 && sender is not ManualLogSource { SourceName: "Preloader" })
        {
            stackTrace = Environment.StackTrace;
        }

        var logHolder = ExtendedLogEventArgs.CreateNewHolder(eventArgs, stackTrace);

        ExtendedLogEventArgs timestampedHolder = null;

        foreach (var listener in SyncListeners)
        {
            if (ShouldEmitLog(listener, logHolder))
            {
                listener.LogEvent(sender, TimestampedListeners.Contains(listener) ? (timestampedHolder ??= logHolder.ToTimestampedEventArgs()) : eventArgs);
            }
        }

        if (Logger.Listeners.Any(l => !SyncListeners.Contains(l)))
        {
            MainWrapper.Schedule(HandleLogEvent, sender, logHolder);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldEmitLog(in ILogListener listener, in ExtendedLogEventArgs eventArgs)
    {
        return UnfilteredListeners.Contains(listener) || !eventArgs.IsFiltered;
    }

    private static void HandleLogEvent(object sender, ExtendedLogEventArgs eventArgs)
    {
        ExtendedLogEventArgs timestampedHolder = null;
        try
        {
            var list = new LinkedList<ILogListener>(Logger.Listeners.Where(l => !SyncListeners.Contains(l)));
            foreach (var listener in list)
            {
                if (!ShouldEmitLog(listener, eventArgs))
                    continue;

                var wrapper = WrappersMap.GetOrAdd(listener, l => new ThreadWrapper($"{l.GetType().Name} Wrapper"));

                wrapper?.Schedule(DispatchLogEvent, sender, TimestampedListeners.Contains(listener) ? (timestampedHolder ??= eventArgs.ToTimestampedEventArgs()) : eventArgs);
                
                continue;

                void DispatchLogEvent(object iSender, ExtendedLogEventArgs iEventHolder)
                {
                    try
                    {
                        listener.LogEvent(iSender, iEventHolder);
                    }
                    catch (Exception ex)
                    {
                        AsyncLoggers.EmergencyLog.LogError($"Exception dispatching log to {listener!.GetType().Name}: {ex}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AsyncLoggers.EmergencyLog.LogError(
                $"Exception dispatching log: {ex}");
        }
    }

    internal class AsyncLogSourceCollection([NotNull] IEnumerable<ILogSource> collection) :
        List<ILogSource>(collection),
        ICollection<ILogSource>
    {
        void ICollection<ILogSource>.Add(ILogSource item)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item", "Log sources cannot be null when added to the source list.");
            }
            item.LogEvent += NewLogEventProcessor;
            Add(item);
        }

        void ICollection<ILogSource>.Clear()
        {
            ILogSource[] array = ToArray();
            foreach (ILogSource item in array)
            {
                ((ICollection<ILogSource>)this).Remove(item);
            }
        }

        bool ICollection<ILogSource>.Remove(ILogSource item)
        {
            if (item == null)
            {
                return false;
            }
            if (!Contains(item))
            {
                return false;
            }
            item.LogEvent -= NewLogEventProcessor;
            Remove(item);
            return true;
        }
    }

}
