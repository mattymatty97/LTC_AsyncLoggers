using System;
using System.Diagnostics.CodeAnalysis;
using AsyncLoggers.Dependency;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace AsyncLoggers
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency("TeamBMX.LobbyCompatibility", BepInDependency.DependencyFlags.SoftDependency)]
    internal class AsyncLoggers : BaseUnityPlugin
    {
        public const string GUID = "mattymatty.AsyncLoggers";
        public const string NAME = "AsyncLoggers";
        public const string VERSION = "1.2.8";

        internal static ManualLogSource Log = AsyncLoggerPreloader.Log;

        [SuppressMessage("ReSharper", "ConvertIfStatementToSwitchStatement")]
        private void Awake()
        {
            try
            {
                    if (LobbyCompatibilityChecker.Enabled)
                        LobbyCompatibilityChecker.Init();
                    
                    Application.quitting += AsyncLoggerPreloader.OnApplicationQuit;
            }
            catch (Exception ex)
            {
                Log.LogError("Exception while initializing: \n" + ex);
            }
        }

    }
}