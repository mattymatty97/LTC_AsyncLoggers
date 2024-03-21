using System.Runtime.CompilerServices;
using LobbyCompatibility.Enums;
using LobbyCompatibility.Features;

namespace AsyncLoggers.Dependency
{
    public static class LobbyCompatibilityChecker
    {
        public static bool Enabled { get { return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("BMX.LobbyCompatibility"); } }
        
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void Init()
        {
            PluginHelper.RegisterPlugin(AsyncLoggers.GUID, System.Version.Parse(AsyncLoggers.VERSION), CompatibilityLevel.ClientOnly, VersionStrictness.Minor);
        }
        
    }
}