using System;
using AsyncLoggers.BepInExListeners;
using AsyncLoggers.Config;
using AsyncLoggers.Proxy;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using Logger = BepInEx.Logging.Logger;

namespace AsyncLoggers.Patches;

[HarmonyPatch]
internal class ChainloaderPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Chainloader), nameof(Chainloader.Initialize))]
    private static void OnInitialize()
    {
        LoggerPatch.SyncListeners.Remove(BepInEx.Preloader.Preloader.PreloaderLog);
                
        ProxyClass.AppendQuittingCallback();
            
        try
        {
            if (SqliteLogger.Enabled)
            {
                AsyncLoggers.Log.LogWarning($"Adding Sqlite to BepInEx Listeners");
                var sqliteListener = new SqliteListener();
                Logger.Listeners.Add(sqliteListener);
                LoggerPatch.UnfilteredListeners.Add(sqliteListener);
            }
        }
        catch (Exception ex)
        {
            AsyncLoggers.Log.LogError($"Exception starting {ex}");
        }
    }


    [HarmonyPatch(typeof(Chainloader), nameof(Chainloader.Initialize)), HarmonyPostfix]
    private static void RegisterListeners()
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
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Logger), nameof(Logger.LogMessage))]
    private static void TrackLoadedPlugins(object data)
    {
        try
        {
            if (data is "Chainloader startup complete")
            {
                SqliteLogger.WriteMods(Chainloader.PluginInfos.Values);
            }
        }
        catch (Exception ex)
        {
            AsyncLoggers.Log.LogError($"Exception logging mods {ex}");
        }
    }
}