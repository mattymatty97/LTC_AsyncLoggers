using System;
using System.Collections.Concurrent;
using AsyncLoggers.StaticContexts;
using AsyncLoggers.Wrappers;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;

namespace AsyncLoggers.Patches
{
    [HarmonyPatch]
    internal class BepInExLoggerPatcher
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Chainloader), nameof(Chainloader.Initialize))]
        private static void OnInitialize()
        {
            GenericContext.PreChainloader = false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BepInEx.Logging.Logger), nameof(BepInEx.Logging.Logger.InternalLogEvent))]
        private static bool wrapLogs(object sender, LogEventArgs eventArgs)
        {
            if (!PluginConfig.BepInEx.Enabled.Value)
                return true;
            
            if (GenericContext.PreChainloader)
                return true;
            
            var uuid = LogContext.Uuid!.Value;
            var timestamp = GenericContext.Timestamp;

            foreach (ILogListener listener in Logger.Listeners)
            {
                var wrapper = GenericContext._wrappersMap.GetOrAdd(listener, (l) => PluginConfig.BepInEx.Scheduler.Value switch
                {
                    PluginConfig.AsyncType.Thread => new ThreadWrapper(),
                    PluginConfig.AsyncType.Job => new JobWrapper(),
                    _ => throw new ArgumentOutOfRangeException()
                });
                
                wrapper.Schedule(() =>
                {
                    try
                    {
                        GenericContext.Async = true;
                        GenericContext.Timestamp = timestamp;
                        LogContext.Uuid = uuid;
                                    
                        if (PluginConfig.Timestamps.Enabled.Value)
                        {
                            BepInExLogEventArgsPatch.CONTEXTS.GetOrCreateValue(eventArgs).Timestamp = AsyncLoggerPreloader.GetLogTimestamp().ToString();
                        }
                        
                        listener?.LogEvent(sender, eventArgs);
                    }
                    catch (Exception ex)
                    {
                        AsyncLoggerPreloader.Log.LogError(
                            $"Exception dispatching log to {listener!.GetType().Name}: {ex}");
                    }
                    finally
                    {
                        GenericContext.Async = false;
                        GenericContext.Timestamp = null;
                        LogContext.Uuid = null;
                    }
                });
            }
            
            return false;
        }
    }
}