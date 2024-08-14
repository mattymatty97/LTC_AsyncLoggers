using BepInEx.Preloader;
using HarmonyLib;

namespace AsyncLoggers.Patches;

[HarmonyPatch]
internal class PreloaderConsoleListenerPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PreloaderConsoleListener), nameof(PreloaderConsoleListener.Dispose))]
    private static void OnDispose(PreloaderConsoleListener __instance)
    {
        LoggerPatch.SyncListeners.Remove(__instance);
        LoggerPatch.UnfilteredListeners.Remove(__instance);
    }
}