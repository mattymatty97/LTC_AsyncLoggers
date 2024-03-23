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
            Application.LogCallback oldHandler = UnityLogSource.OnUnityLogMessageReceived;
            Application.LogCallback newHandler = UnityLogMessageReceivedWrapper;
            EventInfo eventInfo =
                typeof(Application).GetEvent("logMessageReceived", BindingFlags.Static | BindingFlags.Public);
            if (eventInfo != null)
                eventInfo.RemoveEventHandler(null, oldHandler);

            eventInfo = typeof(Application).GetEvent("logMessageReceivedThreaded",
                BindingFlags.Static | BindingFlags.Public);
            if (eventInfo != null)
                eventInfo.AddEventHandler(null, newHandler);
            
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

            void UnityLogMessageReceivedWrapper(string message, string stackTrace, LogType type)
            {
                if (type == LogType.Exception)
                {
                    var customStack = AsyncLoggerPreloader.GetLogStackTrace(2, false);
                    if (customStack != null)
                    {
                        var sections = stackTrace.Trim().Split('\n').ToList();
                        var isJob = sections[sections.Count - 1].StartsWith("Unity.Jobs.JobStruct");
                        var isThread = sections[sections.Count - 1].StartsWith("System.Threading.ThreadHelper");
                        if (isJob || isThread)
                        {
                            var to_remove = 1;
                            for (var i = sections.Count - 2; i >= 0; i--)
                            {
                                var curr = sections[i];
                                if (!curr.StartsWith("AsyncLoggers.Wrappers"))
                                    break;
                                to_remove++;
                            }

                            if (to_remove > 1)
                            {
                                if (to_remove < sections.Count - 2)
                                    sections.RemoveRange(sections.Count - to_remove, to_remove);
                                stackTrace = string.Join("\n", sections) + "\n" + ((string)customStack).Replace("  at ", "") + "\n";
                            }
                        }
                    }
                }
                UnityLogSource.OnUnityLogMessageReceived(message, stackTrace, type);
            }
        }
    }
}