using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AsyncLoggers.Wrappers;
using AsyncLoggers.Wrappers.BepInEx;
using AsyncLoggers.Wrappers.Unity;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace AsyncLoggers
{
    [BepInPlugin(GUID, NAME, VERSION)]
    internal class AsyncLoggers : BaseUnityPlugin
    {
        public const string GUID = "com.github.mattymatty97.AsyncLoggers";
        public const string NAME = "AsyncLoggers";
        public const string VERSION = "1.2.2";

        internal static ManualLogSource Log;

        [SuppressMessage("ReSharper", "ConvertIfStatementToSwitchStatement")]
        private void Awake()
        {
            Log = Logger;
            try
            {
                    PluginConfig.Init();
                
                    if (PluginConfig.Unity.Enabled.Value)
                    {
                        Log.LogWarning("Converting unity logger to async!!");
                        switch (PluginConfig.Unity.Wrapper.Value)
                        {
                            case PluginConfig.UnityWrapperType.LogHandler:
                                Debug.s_Logger.logHandler = new AsyncLogHandlerWrapper(Debug.s_Logger.logHandler);
                                Debug.s_DefaultLogger.logHandler = new AsyncLogHandlerWrapper(Debug.s_DefaultLogger.logHandler);
                                break;
                            case PluginConfig.UnityWrapperType.Logger:
                            {
                                Debug.s_Logger = new AsyncLoggerWrapper(Debug.s_Logger);
                                FieldInfo fieldInfo = typeof(Debug).GetField(nameof(Debug.s_DefaultLogger),
                                    BindingFlags.Static | BindingFlags.NonPublic);
                                fieldInfo?.SetValue(null, new AsyncLoggerWrapper(Debug.s_DefaultLogger));
                                break;
                            }
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        Log.LogInfo("using logMessageReceivedThreaded instead of logMessageReceived for UnityLogSource!!");
                        Application.LogCallback handler = UnityLogSource.OnUnityLogMessageReceived;
                        Application.logMessageReceivedThreaded += handler;
                        Application.logMessageReceived -= handler;
                    }

                    if (PluginConfig.BepInEx.Enabled.Value)
                    {
                        Log.LogWarning("Converting BepInEx loggers to async!!");
                        var list = (List<ILogListener>)BepInEx.Logging.Logger.Listeners;
                        for (var i = 0; i < list.Count; i++)
                        {
                            var logger = list[i];
                            var isConsole = logger is ConsoleLogListener;
                            var isUnity = logger is UnityLogListener;
                            var isDisk = logger is DiskLogListener;
                            
                            if (!isConsole && !isDisk && !isUnity)
                                continue;
                            if(isConsole && !PluginConfig.BepInEx.Console.Value)
                                continue;
                            if(isUnity && !PluginConfig.BepInEx.Unity.Value)
                                continue;
                            if(isDisk && !PluginConfig.BepInEx.Disk.Value)
                                continue;
                            
                            Log.LogWarning($"{logger.GetType().Name} Converted");
                            list[i] = new AsyncLogListenerWrapper(logger);
                        }
                    }

                    Application.wantsToQuit += _OnApplicationQuit;

            }
            catch (Exception ex)
            {
                Log.LogError("Exception while initializing: \n" + ex);
            }
        }

        private bool _OnApplicationQuit()
        {
            OnApplicationQuit();
            return true;
        }
        
        private void OnApplicationQuit()
        {
            Log.LogWarning($"Closing game!");
            JobWrapper.SINGLETON.Stop(PluginConfig.Scheduler.ShutdownType.Value == PluginConfig.ShutdownType.Instant);
            foreach (var logListener in BepInEx.Logging.Logger.Listeners)
            {
                (logListener as AsyncLogListenerWrapper)?.Dispose();
            }
        }

        public static class PluginConfig
        {
            public static void Init()
            {
                var config = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, NAME + ".cfg"), true);
                //Initialize Configs
                
                //Scheduler
                Scheduler.JobBufferSize = config.Bind("Scheduler","job_buffer_size",1024U
                    ,"maximum size of the log queue for the Job Scheduler ( only one Job scheduler exists! )");
                Scheduler.ThreadBufferSize = config.Bind("Scheduler","thread_buffer_size",500U
                    ,"maximum size of the log queue for the Threaded Scheduler ( each logger has a separate one )");
                Scheduler.ShutdownType = config.Bind("Scheduler","shutdown_type",ShutdownType.Instant
                    ,"close immediately or wait for all logs to be written ( Instant/Await ) ");
                //Unity
                Unity.Enabled = config.Bind("Unity","enabled",true
                    ,"convert unity logger to async");
                Unity.Wrapper = config.Bind("Unity","wrapper",UnityWrapperType.Logger
                    ,"wrapper type to use ( Logger/LogHandler )");
                Unity.Scheduler = config.Bind("Unity","scheduler",AsyncType.Job
                    ,"scheduler type to use ( Thread/Job )");
                //BepInEx
                BepInEx.Enabled = config.Bind("BepInEx","enabled",true
                    ,"convert BepInEx loggers to async");
                BepInEx.Disk = config.Bind("BepInEx","disk_wrapper",true
                    ,"convert BepInEx disk logger to async");
                BepInEx.Console = config.Bind("BepInEx","console_wrapper",true
                    ,"convert BepInEx console logger to async");
                BepInEx.Unity = config.Bind("BepInEx","unity_wrapper",true
                    ,"convert BepInEx unity logger to async");                
                BepInEx.Scheduler = config.Bind("BepInEx","scheduler",AsyncType.Thread
                    ,"scheduler type to use ( Thread/Job )");

                //remove unused options
                PropertyInfo orphanedEntriesProp = config.GetType().GetProperty("OrphanedEntries", BindingFlags.NonPublic | BindingFlags.Instance);

                var orphanedEntries = (Dictionary<ConfigDefinition, string>)orphanedEntriesProp!.GetValue(config, null);

                orphanedEntries.Clear(); // Clear orphaned entries (Unbinded/Abandoned entries)
                config.Save(); // Save the config file
            }
            
            public static class Scheduler
            {
                public static ConfigEntry<uint> JobBufferSize;
                public static ConfigEntry<uint> ThreadBufferSize;
                public static ConfigEntry<ShutdownType> ShutdownType;
            }

            public static class Unity
            {
                public static ConfigEntry<bool> Enabled;
                public static ConfigEntry<UnityWrapperType> Wrapper;
                public static ConfigEntry<AsyncType> Scheduler;
            }
            
            [SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
            public static class BepInEx
            {
                public static ConfigEntry<bool> Enabled;
                public static ConfigEntry<bool> Console;
                public static ConfigEntry<bool> Disk;
                public static ConfigEntry<bool> Unity;
                public static ConfigEntry<AsyncType> Scheduler;
            }

            public enum UnityWrapperType
            {
                Logger,
                LogHandler
            }
            
            public enum AsyncType
            {
                Thread,
                Job
            }
            public enum ShutdownType
            {
                Instant,
                Await
            }
        }

    }
}