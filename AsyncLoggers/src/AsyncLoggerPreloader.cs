using System;
using System.Collections.Generic;
using System.Threading;
using AsyncLoggers.Cecil;
using AsyncLoggers.Patches;
using AsyncLoggers.Wrappers;
using AsyncLoggers.Wrappers.BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Mono.Cecil;
using Logger = BepInEx.Logging.Logger;

namespace AsyncLoggers
{
    public static class AsyncLoggerPreloader
    {
        internal static ManualLogSource Log { get; } = Logger.CreateLogSource(nameof(AsyncLoggerPreloader));
        
        public static IEnumerable<string> TargetDLLs { get; } = new string[]{"UnityEngine.CoreModule.dll"};

        internal static int startTime;
        internal static readonly ThreadLocal<object> logTimestamp = new ThreadLocal<object>();

        public static void Patch(AssemblyDefinition assembly)
        {
            if (assembly.Name.Name == "UnityEngine.CoreModule")
            {
                foreach (TypeDefinition type in assembly.MainModule.Types)
                {
                    if (type.FullName == "UnityEngine.Debug")
                    {
                        UnityLogsCecilPatch.PatchUnityLogs(assembly, type);
                    }
                }
            }
        }

        // Cannot be renamed, method name is important
        public static void Initialize()
        {
            PluginConfig.Init();
            Log.LogInfo($"{AsyncLoggers.NAME} Prepatcher Started");
            startTime = Environment.TickCount & Int32.MaxValue;
            if (PluginConfig.Timestamps.Enabled.Value)
                Log.LogWarning($"{AsyncLoggers.NAME} Timestamps start at {DateTime.Now.ToString("dddd, dd MMMM yyyy HH:mm:ss.fffffff")}");
            if (PluginConfig.Timestamps.UseTicks.Value)
                GetCurrTimestamp = () =>
                {
                    if (logTimestamp.IsValueCreated && logTimestamp.Value != null)
                        return logTimestamp.Value;
                    return (Environment.TickCount & Int32.MaxValue) - startTime;
                };
            else
                GetCurrTimestamp = () =>
                {
                    var curr = DateTime.Now.ToString("HH:mm:ss.fffffff");
                    if (logTimestamp.IsValueCreated && logTimestamp.Value != null)
                        return $"{logTimestamp.Value} -> {curr}";
                    return curr;
                };
        }
        
        // Cannot be renamed, method name is important
        public static void Finish()
        {
            Harmony harmony = new Harmony(AsyncLoggers.GUID);
            harmony.PatchAll(typeof(BepInExLogEventArgsPatch));
            //harmony.PatchAll(typeof(UnityLoggerPatcher));
            harmony.PatchAll(typeof(BepInExChainloaderPatch));
            Log.LogInfo($"{AsyncLoggers.NAME} Prepatcher Finished");
        }

        internal static Func<object> GetCurrTimestamp;
        
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