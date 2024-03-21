using System.Collections.Generic;
using AsyncLoggers.Patches;
using AsyncLoggers.Wrappers;
using AsyncLoggers.Wrappers.BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Mono.Cecil;

namespace AsyncLoggers
{
    public static class AsyncLoggerPreloader
    {
        internal static ManualLogSource Log { get; } = BepInEx.Logging.Logger.CreateLogSource(nameof(AsyncLoggerPreloader));
        
        public static IEnumerable<string> TargetDLLs { get; } = new string[0];

        public static void Patch(AssemblyDefinition assembly)
        {
        }
        
        // Cannot be renamed, method name is important
        public static void Finish()
        {
            PluginConfig.Init();
            Log.LogWarning($"{AsyncLoggers.NAME} Prepatcher Started");
            Harmony harmony = new Harmony(AsyncLoggers.GUID);
            harmony.PatchAll(typeof(UnityLoggerPatcher));
            harmony.PatchAll(typeof(BepInExChailoaderPatch));
            Log.LogWarning($"{AsyncLoggers.NAME} Prepatcher Finished");
        }
        
        
        internal static void OnApplicationQuit()
        {
            foreach (var jobWrapper in JobWrapper.INSTANCES.Values)
            {
                jobWrapper.Stop(PluginConfig.Scheduler.ShutdownType.Value == PluginConfig.ShutdownType.Instant);
            }
            foreach (var logListener in Logger.Listeners)
            {
                (logListener as AsyncLogListenerWrapper)?.Dispose();
            }
        }
    }
}