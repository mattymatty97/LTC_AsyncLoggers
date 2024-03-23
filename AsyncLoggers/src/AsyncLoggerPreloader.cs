using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using AsyncLoggers.Cecil;
using AsyncLoggers.Patches;
using AsyncLoggers.Wrappers;
using BepInEx.Logging;
using HarmonyLib;
using Mono.Cecil;
using Logger = BepInEx.Logging.Logger;

namespace AsyncLoggers
{
    public static class AsyncLoggerPreloader
    {
        internal static ManualLogSource Log { get; } = Logger.CreateLogSource(nameof(AsyncLoggerPreloader));
        internal static Harmony _harmony;

        internal static int startTime;
        internal static readonly ThreadLocal<object> logTimestamp = new ThreadLocal<object>();
        internal static readonly ThreadLocal<object> logStackTrace = new ThreadLocal<object>();
        
        public static IEnumerable<string> TargetDLLs { get; } = new string[]{"UnityEngine.CoreModule.dll"};
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
                GetLogTimestamp = () =>
                {
                    if (logTimestamp.IsValueCreated && logTimestamp.Value != null)
                        return logTimestamp.Value;
                    return (Environment.TickCount & Int32.MaxValue) - startTime;
                };
            else
                GetLogTimestamp = () =>
                {
                    if (logTimestamp.IsValueCreated && logTimestamp.Value != null)
                        return logTimestamp.Value;
                    return DateTime.Now.ToString("HH:mm:ss.fffffff");
                };
        }
        
        // Cannot be renamed, method name is important
        public static void Finish()
        {
            _harmony = new Harmony(AsyncLoggers.GUID);
            _harmony.PatchAll(typeof(BepInExLogEventArgsPatch));
            _harmony.PatchAll(typeof(BepInExChainloaderPatch));
            Log.LogInfo($"{AsyncLoggers.NAME} Prepatcher Finished");
        }

        internal static Func<object> GetLogTimestamp;

        internal static object GetLogStackTrace(bool create= false, int skippedCalls = 1)
        {
            if (logStackTrace.IsValueCreated && logStackTrace.Value != null)
                return logStackTrace.Value;
            if (create)
                return new StackTrace(skippedCalls).ToString();
            return null;
        }
        
        internal static void OnApplicationQuit()
        {
            foreach (var jobWrapper in JobWrapper.INSTANCES.Values)
            {
                jobWrapper.Stop(PluginConfig.Scheduler.ShutdownType.Value == PluginConfig.ShutdownType.Instant);
            }
            foreach (var threadWrapper in ThreadWrapper._wrappers)
            {
                threadWrapper?.Stop(PluginConfig.Scheduler.ShutdownType.Value == PluginConfig.ShutdownType.Instant);
            }
        }
    }
}