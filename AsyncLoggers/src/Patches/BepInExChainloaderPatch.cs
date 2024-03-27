using System;
using System.Reflection;
using AsyncLoggers.BepInExListeners;
using AsyncLoggers.Cecil;
using AsyncLoggers.DBAPI;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace AsyncLoggers.Patches
{
    [HarmonyPatch]
    internal class BepInExChainloaderPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Chainloader), nameof(Chainloader.Initialize))]
        private static void AddSqliteListener()
        {
            try
            {
                SqliteLoggerImpl.WriteEvent("BepInEx","Chainloader.Initialize", "Initializing Chainloader");
                if (SqliteLoggerImpl.Enabled)
                {
                    AsyncLoggerPreloader.Log.LogWarning($"Adding Sqlite to BepInEx Listeners");
                    BepInEx.Logging.Logger.Listeners.Add(new SqliteListener());
                }
                ProxyClass.AppendQuittingCallback();
            }
            catch (Exception ex)
            {
                AsyncLoggerPreloader.Log.LogError($"Exception starting {ex}");
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Chainloader), nameof(Chainloader.Initialize))]
        private static void UseAsyncLogListeners()
        {
            try
            {
                SqliteLoggerImpl.WriteEvent("BepInEx", "Chainloader.Initialize", "Finished Initializing Chainloader");
                
                Application.LogCallback handler = UnityLogSource.OnUnityLogMessageReceived;
                EventInfo eventInfo =
                    typeof(Application).GetEvent("logMessageReceived", BindingFlags.Static | BindingFlags.Public);
                if (eventInfo != null)
                    eventInfo.RemoveEventHandler(null, handler);

                eventInfo = typeof(Application).GetEvent("logMessageReceivedThreaded",
                    BindingFlags.Static | BindingFlags.Public);
                if (eventInfo != null)
                    eventInfo.AddEventHandler(null, handler);
                
                /*
                if (PluginConfig.BepInEx.Enabled.Value)
                {
                    AsyncLoggerPreloader.Log.LogWarning("Converting BepInEx loggers to async!!");
                    var collection = BepInEx.Logging.Logger.Listeners;
                    var list = collection.ToList();
                    foreach (var logger in list)
                    {
                        var isConsole = logger is ConsoleLogListener;
                        var isUnity = logger is UnityLogListener;
                        var isDisk = logger is DiskLogListener;

                        if (!isConsole && !isDisk && !isUnity)
                            continue;
                        if (isConsole && !PluginConfig.BepInEx.Console.Value)
                            continue;
                        if (isUnity && !PluginConfig.BepInEx.Unity.Value)
                            continue;
                        if (isDisk && !PluginConfig.BepInEx.Disk.Value)
                            continue;

                        AsyncLoggerPreloader.Log.LogWarning($"{logger.GetType().Name} Converted");
                        collection.Remove(logger);
                        collection.Add(new AsyncLogListenerWrapper(logger));
                    }
                }*/
            }
            catch (Exception ex)
            {
                AsyncLoggerPreloader.Log.LogError($"Exception while converting BepInEx loggers to async! {ex}");
            }
        }
        
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Logger), nameof(Logger.LogMessage))]
        private static void OnStart(object data)
        {
            try
            {   
                if (data is"Chainloader started")
                    SqliteLoggerImpl.WriteEvent("BepInEx", "Chainloader.Start", "Starting Chainloader");
            }
            catch (Exception ex)
            {
                AsyncLoggerPreloader.Log.LogError($"Exception logging event {ex}");
            }
        }
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Logger), nameof(Logger.LogMessage))]
        private static void TrackLoadedPlugins(object data)
        {
            try
            {
                if (data is "Chainloader startup complete")
                {
                    SqliteLoggerImpl.WriteEvent("BepInEx", "Chainloader.Start", "Finished Starting Chainloader");
                    SqliteLoggerImpl.WriteMods(Chainloader.PluginInfos.Values);
                }
            }
            catch (Exception ex)
            {
                AsyncLoggerPreloader.Log.LogError($"Exception logging mods {ex}");
            }
        }
    }
}