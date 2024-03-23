using System.Linq;
using System.Reflection;
using AsyncLoggers.Wrappers.BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace AsyncLoggers.Patches
{
    [HarmonyPatch]
    internal class BepInExChainloaderPatch
    {
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Chainloader), nameof(Chainloader.Initialize))]
        private static void UseAsyncLogListeners()
        {
            AsyncLoggerPreloader.Log.LogInfo(
                "using logMessageReceivedThreaded instead of logMessageReceived for UnityLogSource!!");
            Application.LogCallback handler = UnityLogSource.OnUnityLogMessageReceived;
            EventInfo eventInfo =
                typeof(Application).GetEvent("logMessageReceived", BindingFlags.Static | BindingFlags.Public);
            if (eventInfo != null)
                eventInfo.RemoveEventHandler(null, handler);

            eventInfo = typeof(Application).GetEvent("logMessageReceivedThreaded",
                BindingFlags.Static | BindingFlags.Public);
            if (eventInfo != null)
                eventInfo.AddEventHandler(null, handler);
            
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
            }
        }
    }
}