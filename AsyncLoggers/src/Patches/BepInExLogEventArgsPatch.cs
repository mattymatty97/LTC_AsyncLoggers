using System;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using HarmonyLib;

namespace AsyncLoggers.Patches
{
    [HarmonyPatch]
    internal class BepInExLogEventArgsPatch
    {
        internal static readonly ConditionalWeakTable<LogEventArgs, LogEventContext> Contexts = new();
        
        internal class LogEventContext
        {
            internal string Timestamp { get; set;}
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LogEventArgs), nameof(LogEventArgs.ToString))]
        private static void PrependTimestamp(LogEventArgs __instance, ref string __result)
        {
            if (PluginConfig.Timestamps.Enabled.Value && 
                Contexts.TryGetValue(__instance, out var logContext))
            {
                __result = $"[{logContext.Timestamp}] {__result}";
            }
        }
    }
}