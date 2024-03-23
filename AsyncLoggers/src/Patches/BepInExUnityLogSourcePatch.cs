using BepInEx.Logging;
using HarmonyLib;

namespace AsyncLoggers.Patches
{
    [HarmonyPatch]
    internal class BepInExUnityLogSourcePatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UnityLogSource), nameof(UnityLogSource.OnUnityLogMessageReceived))]
        private static void UseCustomStackTrace(ref string stackTrace)
        {
            stackTrace = stackTrace + "\n\nTest\n\n";
        }
    }
}