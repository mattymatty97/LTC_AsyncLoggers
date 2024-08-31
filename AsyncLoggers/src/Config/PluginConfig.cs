using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Xml;
using AsyncLoggers.API;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace AsyncLoggers.Config;

[SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
internal static class PluginConfig
{
    private static ConfigFile _config;
    internal static ConfigFile FilterConfig;
    internal static XmlDocument LogConfig;


    internal static void Init()
    {
        _config = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, AsyncLoggers.NAME, "Config.cfg"), true, AsyncLoggers.Plugin);
        FilterConfig = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, AsyncLoggers.NAME, "LogLevels.cfg"), true, AsyncLoggers.Plugin);
        XmlConfig.InitLogConfig();

        //Initialize Configs
        //LogWrapping
        LogWrapping.Enabled = _config.Bind("LogWrapping", "Enabled", 
            false, "Look into assemblies and tweak calls to Unity.Debug");
        LogWrapping.TargetGameAssemblies = _config.Bind("LogWrapping", "Target Game Assemblies",
            "Assembly-CSharp.dll,Other-assembly.dll", "Which game assemblies to look into");
        //Timestamps
        Timestamps.Enabled = _config.Bind("Timestamps", "Enabled", true
            , "add numeric timestamps to the logs");
        Timestamps.Type = _config.Bind("Timestamps", "Type", TimestampType.DateTime
            , "what kind of timestamps to use");
        //DbLogger
        DbLogger.Enabled = _config.Bind("DbLogger", "Enabled", true
            , "flush logs to a Sqlite database");
        DbLogger.RotationSize = _config.Bind("DbLogger", "Min file size for rotation", 100000000L
            , "how big the file can grow before it is rotated ( in bytes )");
        //Scheduler
        Scheduler.ThreadBufferSize = _config.Bind("Scheduler", "Buffer max size", 500U
            , "maximum size of the log queue for the Threaded Scheduler ( each logger has a separate one )");
        Scheduler.ShutdownType = _config.Bind("Scheduler", "Shutdown style", ShutdownType.Await
            , "close immediately or wait for all logs to be written ( Instant/Await ) ");
        //BepInEx
        BepInEx.Disk = _config.Bind("BepInEx", "Async File", true
            , "convert BepInEx disk writer to async");
        BepInEx.Console = _config.Bind("BepInEx", "Async Console", true
            , "convert BepInEx console to async");
        BepInEx.Unity = _config.Bind("BepInEx", "Async Unity", true
            , "convert BepInEx->Unity Log to async");
        BepInEx.Traces = _config.Bind("BepInEx", "Do not collect StackTraces", false
            , "by default AsyncLoggers will block and collect StackTraces for Error and Fatal");
        //Debug
        Debug.LogWrappingVerbosity = _config.Bind("Debug", "LogWrapping Verbosity Level", LogLevel.None,
            "Print A LOT more logs about LogWrapping");

        if (BepInEx.Traces.Value)
            AsyncLoggersAPI.TraceableLevelsMask = LogLevel.None;

        CleanOrphanedEntries(_config);
    }

    internal static void CleanOrphanedEntries(ConfigFile config)
    {
        //remove unused options
        var orphanedEntriesProp =
            config.GetType().GetProperty("OrphanedEntries", BindingFlags.NonPublic | BindingFlags.Instance);

        var orphanedEntries = (Dictionary<ConfigDefinition, string>)orphanedEntriesProp!.GetValue(config, null);

        orphanedEntries.Clear(); // Clear orphaned entries (Unbinded/Abandoned entries)
        config.Save(); // Save the config file
    }

    public static class Scheduler
    {
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

    public static class BepInEx
    {
        public static ConfigEntry<bool> Console;
        public static ConfigEntry<bool> Disk;
        public static ConfigEntry<bool> Unity;
        public static ConfigEntry<bool> Traces;
    }

    public static class LogWrapping
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<string> TargetGameAssemblies;
    }

    public static class Debug
    {
        public static ConfigEntry<LogLevel> LogWrappingVerbosity;
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
        FrameCount,
        Counter
    }
}