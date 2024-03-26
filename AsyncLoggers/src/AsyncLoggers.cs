using System;
using System.Diagnostics.CodeAnalysis;
using AsyncLoggers.Dependency;
using BepInEx;
using UnityEngine;

namespace AsyncLoggers
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency("TeamBMX.LobbyCompatibility", BepInDependency.DependencyFlags.SoftDependency)]
    internal class AsyncLoggers : BaseUnityPlugin
    {
        public const string GUID = "mattymatty.AsyncLoggers";
        public const string NAME = "AsyncLoggers";
        public const string VERSION = "1.6.0";

        [SuppressMessage("ReSharper", "ConvertIfStatementToSwitchStatement")]
        private void Awake()
        {
            try
            {
                if (LobbyCompatibilityProxy.Enabled)
                    LobbyCompatibilityProxy.Init();
                
                Application.quitting += AsyncLoggerPreloader.OnApplicationQuit;
            }
            catch (Exception ex)
            {
                AsyncLoggerPreloader.Log.LogError("Exception while initializing: \n" + ex);
            }
        }

    }
}