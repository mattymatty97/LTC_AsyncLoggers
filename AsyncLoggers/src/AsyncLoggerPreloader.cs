using System;
using System.Collections.Generic;
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
        internal static ManualLogSource Log { get; } = Logger.CreateLogSource(nameof(AsyncLoggers));
        internal static Harmony _harmony;

        internal static int startTime;
        
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
            switch (PluginConfig.Timestamps.Type.Value)
            {
                case PluginConfig.TimestampType.DateTime:
                    GetLogTimestamp = () =>
                    {
                        var timestamp = LogContext.Timestamp;
                        return timestamp.Substring(timestamp.Length - 16);
                    };
                    break;
                case PluginConfig.TimestampType.TickCount:
                    GetLogTimestamp = () =>
                    {
                        var timestamp = $"{(Environment.TickCount & Int32.MaxValue) - startTime:0000000000000000}";
                        return timestamp.Substring(timestamp.Length - 16);
                    };
                    break;
                case PluginConfig.TimestampType.Counter:
                    GetLogTimestamp = () =>
                    {
                        var timestamp = $"{LogContext.Uuid:0000000000000000}";
                        return timestamp.Substring(timestamp.Length - 16);
                    };
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"{PluginConfig.Timestamps.Type.Value} is not a valid TimestampType");
            }
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