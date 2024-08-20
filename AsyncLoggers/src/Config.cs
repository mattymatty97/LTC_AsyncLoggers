using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using File = System.IO.File;

namespace AsyncLoggers;

using System;
using System.Xml;

internal struct LogCallInfo
{
    public string AssemblyName { get; }
    public string ClassName { get; }
    public string MethodName { get; }
    public string LogLine { get; }

    public readonly CallStatus Status => _status != CallStatus.Default ? _status : DefaultStatus;

    public int? Delay { get; }

    private LogCallInfo(string assemblyName, string className, string methodName, string logLine, CallStatus status,
        int? delay)
    {
        AssemblyName = assemblyName;
        ClassName = className;
        MethodName = methodName;
        LogLine = logLine;
        _status = status;
        Delay = delay;
    }

    internal static LogCallInfo GetOrAdd(string assemblyName, string className, string methodName, string logLine)
    {
        var doc = PluginConfig.LogConfig; // Assuming PluginConfig.LogConfig is an XmlDocument
        var root = doc.DocumentElement;

        // Find or create the Assembly node
        XmlNode assemblyNode = root!.SelectSingleNode($"Assembly[@name='{assemblyName}']");
        if (assemblyNode == null)
        {
            assemblyNode = doc.CreateElement("Assembly");
            var assemblyAttr = doc.CreateAttribute("name");
            assemblyAttr.Value = assemblyName;
            ((XmlElement)assemblyNode).SetAttributeNode(assemblyAttr);
            root.AppendChild(assemblyNode);
        }

        // Find or create the Class node
        XmlNode classNode = assemblyNode.SelectSingleNode("Class[@name='" + className + "']");
        if (classNode == null)
        {
            classNode = doc.CreateElement("Class");
            var classAttr = doc.CreateAttribute("name");
            classAttr.Value = className;
            ((XmlElement)classNode).SetAttributeNode(classAttr);
            assemblyNode.AppendChild(classNode);
        }

        // Find or create the Method node
        XmlNode methodNode = classNode.SelectSingleNode("Method[@name='" + methodName + "']");
        if (methodNode == null)
        {
            methodNode = doc.CreateElement("Method");
            var methodAttr = doc.CreateAttribute("name");
            methodAttr.Value = methodName;
            ((XmlElement)methodNode).SetAttributeNode(methodAttr);
            classNode.AppendChild(methodNode);
        }

        // Find or create the LogCall node
        XmlNode logCallNode = null;
        foreach (XmlNode node in methodNode!.SelectNodes("LogCall")!)
        {
            if (node.InnerText.Trim() == logLine.Trim())
            {
                logCallNode = node;
                break;
            }
        }

        if (logCallNode == null)
        {
            // Create new LogCall element
            logCallNode = doc.CreateElement("LogCall");
            var statusAttr = doc.CreateAttribute("status");
            statusAttr.Value = "Default";
            ((XmlElement)logCallNode).SetAttributeNode(statusAttr);
            logCallNode.InnerText = logLine;

            // Add comments
            XmlComment comment1 =
                doc.CreateComment(
                    $"Valid values for \"status\" are: [{string.Join(", ", Enum.GetNames(typeof(CallStatus)))}]");
            XmlComment comment2 =
                doc.CreateComment(
                    $"If status is \"{nameof(CallStatus.BepInEx)}\" you can add an extra \"delay\" attribute ( integer value in ms ) to throttle the prints");

            methodNode.AppendChild(comment1);
            methodNode.AppendChild(comment2);
            methodNode.AppendChild(logCallNode);
        }

        // Parse the status and delay attributes
        var statusAttrValue = ((XmlElement)logCallNode).GetAttribute("status");
        if (!Enum.TryParse(statusAttrValue ?? "Default", out CallStatus status))
            status = CallStatus.Default;

        int? delay = null;
        var delayAttrValue = ((XmlElement)logCallNode).GetAttribute("delay");
        if (!delayAttrValue.IsNullOrWhiteSpace())
        {
            if (int.TryParse(delayAttrValue, out var parsedDelay))
                delay = parsedDelay;
            else
                delay = 1000; // Default value if parsing fails
        }

        return new LogCallInfo(assemblyName, className, methodName, logLine, status, delay);
    }

    internal static CallStatus DefaultStatus = CallStatus.Unity;
    private readonly CallStatus _status;


    internal enum CallStatus
    {
        Default,
        Unity,
        BepInEx,
        Suppressed
    }
}

internal static class FilterConfig
{
    internal static readonly ConcurrentDictionary<ILogSource, LogLevel> LevelMasks = [];

    private static LogLevel GetListenerConfig(ILogSource source)
    {
        var sourceName = source.SourceName.Trim();

        var sectionName = Regex.Replace(sourceName, @"[\n\t\\\'[\]]", "");

        var enabled = PluginConfig.FilterConfig.Bind(sectionName, "Enabled", true,
            new ConfigDescription("Allow source to write logs"));

        var logLevel = PluginConfig.FilterConfig.Bind(sectionName, "LogLevels", LogLevel.All,
            new ConfigDescription("What levels to write"));

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
    internal static XmlDocument LogConfig;

    private static void InitLogConfig()
    {
        var file = Utility.CombinePaths(Paths.ConfigPath, AsyncLoggers.NAME, "LogConfig.xml");

        // Initialize XmlDocument
        LogConfig = new XmlDocument();

        if (File.Exists(file))
        {
            // Load existing XML file
            LogConfig.Load(file);
        }
        
        if(LogConfig.DocumentElement == null)
        {
            // Create new XML document with root element
            var root = LogConfig.CreateElement("Configuration");
            var attr = LogConfig.CreateAttribute("default_status");
            attr.Value = LogCallInfo.DefaultStatus.ToString();
            root.SetAttributeNode(attr);
            LogConfig.AppendChild(root);
        }

        if (Enum.TryParse(LogConfig.DocumentElement!.GetAttribute("default_state"), out LogCallInfo.CallStatus defaultStatus) && defaultStatus != LogCallInfo.CallStatus.Default)
        {
            LogCallInfo.DefaultStatus = defaultStatus;
        }
        
    }

    internal static void WriteLogConfig()
    {
        var file = Utility.CombinePaths(Paths.ConfigPath, AsyncLoggers.NAME, "LogConfig.xml");

        // Write to the file
        using (var writer = new XmlTextWriter(file, Encoding.UTF8))
        {
            writer.Formatting = Formatting.Indented;
            LogConfig.WriteContentTo(writer);
        }
    }

    internal static void Init()
    {
        _config = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, AsyncLoggers.NAME, "Config.cfg"), true);
        FilterConfig = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, AsyncLoggers.NAME, "LogLevels.cfg"), true);
        InitLogConfig();

        //Initialize Configs
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
        BepInEx.Unity = _config.Bind("BepInEx", "Async Unity", false
            , "convert BepInEx->Unity Log to async");
        //Debug
        Debug.Verbose = _config.Bind("Debug", "Verbose", false, "Print A LOT more logs");

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
    }

    public static class Debug
    {
        public static ConfigEntry<bool> Verbose;
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