using System;
using System.Reflection;
using System.Threading;
using AsyncLoggers.BepInExListeners;
using AsyncLoggers.Config;
using AsyncLoggers.Proxy;
using AsyncLoggers.Wrappers;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace AsyncLoggers.Patches;

[HarmonyPatch]
internal class ChainloaderPatch
{


    [HarmonyPrefix]
    [HarmonyPatch(typeof(Chainloader), nameof(Chainloader.Initialize))]
    private static void InitializePrefix()
    {
        AsyncLoggers.EmergencyLog.LogWarning("a");
        try
        {
            var sourcesField =
                typeof(Logger).GetField("<Sources>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);

            var oldSourcesList = Logger.Sources;
            var newSourcesList = new LoggerPatch.AsyncLogSourceCollection(oldSourcesList);
            sourcesField!.SetValue(null, newSourcesList);
        }
        catch (Exception ex)
        {
            AsyncLoggers.EmergencyLog.LogError($"Exception replacing sources list {ex}");
        }

        AsyncLoggers.EmergencyLog.LogWarning("b");
        try
        {
            if (SqliteLogger.Enabled)
            {
                AsyncLoggers.Log.LogWarning("Adding Sqlite to BepInEx Listeners");
                var sqliteListener = new SqliteListener();
                var wrapper = LoggerPatch.WrappersMap.GetOrAdd(sqliteListener,
                    l => new ThreadWrapper($"{l.GetType().Name} Wrapper"));
                wrapper.OnBecomeIdle += sqliteListener.FlushWAL;
                wrapper.Stopping += sqliteListener.FlushWAL;
                wrapper.Stopping += () =>
                    SqliteLogger.Terminate(PluginConfig.Scheduler.ShutdownType.Value ==
                                           PluginConfig.ShutdownType.Instant);

                Logger.Listeners.Add(sqliteListener);
                LoggerPatch.UnfilteredListeners.Add(sqliteListener);
            }
        }
        catch (Exception ex)
        {
            AsyncLoggers.EmergencyLog.LogError($"Exception adding sqlite listener {ex}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Chainloader), nameof(Chainloader.Initialize))]
    private static void InitializePostfix()
    {
        AsyncLoggers.EmergencyLog.LogWarning("c");

        foreach (var listener in Logger.Listeners)
        {
            switch (listener)
            {
                case UnityLogListener:
                    if (!PluginConfig.BepInEx.Unity.Value)
                        LoggerPatch.SyncListeners.Add(listener);
                    LoggerPatch.UnfilteredListeners.Add(listener);
                    break;
                case DiskLogListener:
                    if (!PluginConfig.BepInEx.Disk.Value)
                        LoggerPatch.SyncListeners.Add(listener);
                    LoggerPatch.TimestampedListeners.Add(listener);
                    break;
                case ConsoleLogListener:
                    if (!PluginConfig.BepInEx.Console.Value)
                        LoggerPatch.SyncListeners.Add(listener);
                    LoggerPatch.TimestampedListeners.Add(listener);
                    break;
            }
        }

        AsyncLoggers.EmergencyLog.LogWarning("d");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Chainloader), nameof(Chainloader.Start))]
    private static void StartPrefix()
    {
        try
        {
            ProxyClass.AppendQuittingCallback();

            var threadedEvent = typeof(Application).GetEvent("logMessageReceivedThreaded", BindingFlags.Static | BindingFlags.Public);
            if (threadedEvent != null)
            {
                var @delegate = new Application.LogCallback(OnThreadedUnityLog);

                threadedEvent.AddEventHandler(null, @delegate);
            }
        }
        catch (Exception ex)
        {
            AsyncLoggers.EmergencyLog.LogError($"Exception in Chainloader.Start prefix {ex}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Chainloader), nameof(Chainloader.Start))]
    private static void StartPostfix()
    {
        if (!Chainloader._loaded)
            return;

        try
        {
            SqliteLogger.WriteMods(Chainloader.PluginInfos.Values);
        }
        catch (Exception ex)
        {
            AsyncLoggers.EmergencyLog.LogError($"Exception logging mods {ex}");
        }
    }

    private static void OnThreadedUnityLog(string message, string stackTrace, LogType logType)
    {
        if (Thread.CurrentThread.ManagedThreadId != AsyncLoggers.MainThreadID)
            UnityLogSource.OnUnityLogMessageReceived(message, stackTrace, logType);
    }
}
