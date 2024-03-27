using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;

namespace AsyncLoggers
{
    public static class PluginConfig
    {
        public static void Init()
        {
            var config = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, AsyncLoggerPreloader.NAME + ".cfg"), true);
            //Initialize Configs
            //Timestamps
            Timestamps.Enabled = config.Bind("Timestamps","enabled",true
                ,"add numeric timestamps to the logs");
            Timestamps.Type = config.Bind("Timestamps","type",TimestampType.DateTime
                ,"what kind of timestamps to use");            
            //DbLogger
            DbLogger.Enabled = config.Bind("DbLogger","enabled",true
                ,"flush logs to a Sqlite database");
            DbLogger.RotationSize = config.Bind("DbLogger","rotation_size", 100000000L
                ,"how big the file can grow before it is rotated ( in bytes )");
            //Scheduler
            Scheduler.JobBufferSize = config.Bind("Scheduler","job_buffer_size",1024U
                ,"maximum size of the log queue for the Job Scheduler ( only one Job scheduler exists! )");
            Scheduler.ThreadBufferSize = config.Bind("Scheduler","thread_buffer_size",500U
                ,"maximum size of the log queue for the Threaded Scheduler ( each logger has a separate one )");
            Scheduler.ShutdownType = config.Bind("Scheduler","shutdown_type",ShutdownType.Await
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
        
        public static class DbLogger
        {
            public static ConfigEntry<bool> Enabled;
            public static ConfigEntry<long> RotationSize;
        }
        
        public static class Timestamps
        {
            public static ConfigEntry<bool> Enabled;
            public static ConfigEntry<TimestampType> Type;
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
        
        public enum TimestampType
        {
            DateTime,
            TickCount,
            Counter
        }
    }
}