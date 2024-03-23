using System.Diagnostics;
using BepInEx.Logging;
using HarmonyLib;

namespace AsyncLoggers.Patches
{
    [HarmonyPatch]
    internal class BepInExLogEventArgsPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LogEventArgs), nameof(LogEventArgs.ToString))]
        private static void PrependTimestamp(LogEventArgs __instance, ref string __result)
        {
            if (PluginConfig.Timestamps.Enabled.Value)
            {
                var timestamp = AsyncLoggerPreloader.GetCurrTimestamp();
                __result = $"[{timestamp}] {__result}";
            }
        }
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(LogEventArgs), "set_Data")]
        private static void AppendStackTrace(ref object __0)
        {
            var stackTrace = new StackTrace();
            __0 = __0 + "\n" + stackTrace.ToString();
        }
    }
}