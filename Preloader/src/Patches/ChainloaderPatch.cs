using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using AsyncLoggers.BepInExListeners;
using AsyncLoggers.Config;
using AsyncLoggers.Proxy;
using AsyncLoggers.Wrappers;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace AsyncLoggers.Patches;

internal class ChainloaderPatch
{
    //REMINDER!!: do not patch Chainloader.Start!!!!
    
    private static Hook _chainloaderLoadedHook;

    internal static void InitMonoMod()
    {
        var methodInfo =
            AccessTools.Method(typeof(Chainloader), nameof(Chainloader.Initialize));
        
        AsyncLoggers.Hooks.Add(new Hook(methodInfo, DetourInitialize));
        
        methodInfo =
            AccessTools.Method(typeof(Logger), nameof(Logger.LogMessage));

        _chainloaderLoadedHook = new Hook(methodInfo, DetourLogMessage);
        AsyncLoggers.Hooks.Add(_chainloaderLoadedHook);
    }

    private static void DetourInitialize(Action<string, bool, ICollection<LogEventArgs>> orig, 
        string gameExePath,
        bool startConsole,
        ICollection<LogEventArgs> preloaderLogEvents)
    {
        InitializePrefix();
        orig(gameExePath, startConsole, preloaderLogEvents);
        InitializePostfix();
    }
    
    private static void DetourLogMessage(Action<object> orig, object data)
    {
        orig(data);

        if (data is string message && message.Equals("Chainloader startup complete"))
        {
            _chainloaderLoadedHook?.Dispose();
            AsyncLoggers.Hooks.Remove(_chainloaderLoadedHook);
            try
            {
                SqliteLogger.WriteMods(Chainloader.PluginInfos.Values);
            }
            catch (Exception ex)
            {
                AsyncLoggers.EmergencyLog.LogError($"Exception logging mods {ex}");
            }
        }
    }
    
    private static void InitializePrefix()
    {
        try
        {
            var sourcesField =
                typeof(Logger).GetField("<Sources>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);

            var oldSourcesList = Logger.Sources;
            var newSourcesList = new LoggerPatch.AsyncLogSourceCollection(oldSourcesList);
            sourcesField!.SetValue(null, newSourcesList);
            oldSourcesList.Clear();
        }
        catch (Exception ex)
        {
            AsyncLoggers.EmergencyLog.LogError($"Exception replacing sources list {ex}");
        }

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

    private static void InitializePostfix()
    {

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
        
        ProxyClass.AppendQuittingCallback();
        
        try
        {
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

    private static void OnThreadedUnityLog(string message, string stackTrace, LogType logType)
    {
        if (Thread.CurrentThread.ManagedThreadId != AsyncLoggers.MainThreadID)
            UnityLogSource.OnUnityLogMessageReceived(message, stackTrace, logType);
    }
}
