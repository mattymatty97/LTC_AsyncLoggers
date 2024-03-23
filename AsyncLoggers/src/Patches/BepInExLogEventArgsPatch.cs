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
    }
}