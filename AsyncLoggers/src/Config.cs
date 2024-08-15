using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace AsyncLoggers;

internal static class FilterConfig
{
    internal static readonly ConcurrentDictionary<ILogSource, LogLevel> LevelMasks = [];

    private static LogLevel GetListenerConfig(ILogSource source)
    {
        var sourceName = source.SourceName.Trim();

        var sectionName = Regex.Replace(sourceName, @"[\n\t\\\'[\]]", "");

        var enabled = PluginConfig.FilterConfig.Bind(sectionName, "Enabled", true,
            new ConfigDescription("Allow source to write logs"));
        
        var logLevel = PluginConfig.FilterConfig.Bind(sectionName, "LogLevels", LogLevel.All, new ConfigDescription("What levels to write"));

        return enabled.Value ? logLevel.Value : LogLevel.None;
    }
    
    internal static LogLevel GetMaskForSource(ILogSource source)
    {
        return LevelMasks.GetOrAdd(source, GetListenerConfig);
    }
    
}

[SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
internal static class PluginConfig
{
    private static ConfigFile _config;
    internal static ConfigFile FilterConfig;
        
    internal static void Init()
    {
        _config = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, $"{AsyncLoggers.NAME}.cfg"), true);
        FilterConfig = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, $"{AsyncLoggers.NAME}.Filter.cfg"), true);
        //Initialize Configs
        //Timestamps
        Timestamps.Enabled = _config.Bind("Timestamps","Enabled",true
            ,"add numeric timestamps to the logs");
        Timestamps.Type = _config.Bind("Timestamps","Type",TimestampType.DateTime
            ,"what kind of timestamps to use");            
        //DbLogger
        DbLogger.Enabled = _config.Bind("DbLogger","Enabled",true
            ,"flush logs to a Sqlite database");
        DbLogger.RotationSize = _config.Bind("DbLogger","Min file size for rotation", 100000000L
            ,"how big the file can grow before it is rotated ( in bytes )");
        //Scheduler
        Scheduler.ThreadBufferSize = _config.Bind("Scheduler","Buffer max size",500U
            ,"maximum size of the log queue for the Threaded Scheduler ( each logger has a separate one )");
        Scheduler.ShutdownType = _config.Bind("Scheduler","Shutdown style",ShutdownType.Await
            ,"close immediately or wait for all logs to be written ( Instant/Await ) ");
        //BepInEx
        BepInEx.Enabled = _config.Bind("BepInEx","Enabled",true
            ,"convert BepInEx loggers to async");
        BepInEx.Disk = _config.Bind("BepInEx","Async File",true
            ,"convert BepInEx disk writer to async");
        BepInEx.Console = _config.Bind("BepInEx","Async Console",true
            ,"convert BepInEx console to async");
        BepInEx.Unity = _config.Bind("BepInEx","Async Unity",false
            ,"convert BepInEx->Unity Log to async");

        CleanOrphanedEntries(_config);
    }

    internal static void CleanOrphanedEntries(ConfigFile config)
    {
        //remove unused options
        var orphanedEntriesProp = config.GetType().GetProperty("OrphanedEntries", BindingFlags.NonPublic | BindingFlags.Instance);

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
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<bool> Console;
        public static ConfigEntry<bool> Disk;
        public static ConfigEntry<bool> Unity;
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