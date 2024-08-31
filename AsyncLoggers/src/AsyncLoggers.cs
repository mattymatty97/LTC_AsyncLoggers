using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using AsyncLoggers.BepInExListeners;
using AsyncLoggers.Cecil;
using AsyncLoggers.Config;
using AsyncLoggers.Patches;
using AsyncLoggers.Wrappers;
using AsyncLoggers.Wrappers.EventArgs;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using Mono.Cecil;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace AsyncLoggers;

public static class AsyncLoggers
{
    public const string GUID = "mattymatty.AsyncLoggers";
    public const string NAME = "AsyncLoggers";
    public const string VERSION = "2.1.2";

    internal static readonly BepInPlugin Plugin = new BepInPlugin(GUID, NAME, VERSION);
    internal static ManualLogSource Log { get; } = Logger.CreateLogSource(nameof(AsyncLoggers));
    internal static ManualLogSource WrappedUnitySource { get; } = Logger.CreateLogSource("Unity Log");

    internal static Harmony _harmony;

    internal static int _startTime { get; private set; }

    public static IEnumerable<string> TargetDLLs => GetDLLs();

    private static IEnumerable<string> GetDLLs()
    {
        if (!PluginConfig.LogWrapping.Enabled.Value)
            yield break;

        var dlls = Utility.GetUniqueFilesInDirectories(BepInEx.Paths.DllSearchPaths, "*.dll");

        var assemblies = PluginConfig.LogWrapping.TargetGameAssemblies.Value.Split(",").Select(p => p.Trim()).ToList();
        ;
        var sortedList = AssemblyDependencySorter.SortAssembliesByDependency(assemblies.Select(name =>
        {
            return dlls.FirstOrDefault(path => path.EndsWith(name));
        }).Where(s => s != null)).ToList();

        if (assemblies.Contains("***"))
            sortedList = AssemblyDependencySorter
                .SortAssembliesByDependency(dlls.Where(an => !an.Contains("netstandard"))).ToList();

        VerboseLogWrappingLog(LogLevel.Warning,
            () => $"Assembly Load Order: {string.Join(",", sortedList.Select(Path.GetFileName))}");

        foreach (var assembly in sortedList)
        {
            yield return Path.GetFileName(assembly);
        }
    }

    public static void Patch(AssemblyDefinition assembly)
    {
        try
        {
            AsyncLoggers.Log.LogWarning($"Parsing {assembly.Name.Name} for Unity.Debug calls!");
            AssemblyAnalyzer.ProcessAssembly(assembly, out var count);
            AsyncLoggers.Log.LogInfo($"Found {count} Unity.Debug calls in {assembly.Name.Name}");
        }
        catch (Exception ex)
        {
            AsyncLoggers.Log.LogFatal(ex);
        }
    }

    private static int _lastUnityFrame = 0;
    private static bool _quitting = false;

    public static int UnityFrameCount
    {
        get
        {
            int frame;
            if (!_quitting){
                frame = Time.frameCount;
                _lastUnityFrame = frame;
            }
            else
            {
                frame = _lastUnityFrame;
            }

            return frame;
        }
    }

    // Cannot be renamed, method name is important
    public static void Initialize()
    {
        Log.LogInfo($"{NAME}:{VERSION} Prepatcher Started");
        PluginConfig.Init();

        _startTime = Environment.TickCount & Int32.MaxValue;
        if (PluginConfig.Timestamps.Enabled.Value)
            Log.LogWarning(
                $"{NAME}:{VERSION} Timestamps start at {DateTime.UtcNow:dddd, dd MMMM yyyy HH:mm:ss.fffffff} UTC");

        GetLogTimestamp = PluginConfig.Timestamps.Type.Value switch
        {
            PluginConfig.TimestampType.DateTime => (le) =>
            {
                var context = le.AsLogEventWrapper();
                var timestamp = context.Timestamp;
                return timestamp.ToString("HH:mm:ss.fffffff");
            },
            PluginConfig.TimestampType.TickCount => (le) =>
            {
                var context = le.AsLogEventWrapper();
                var timestamp = $"{context.Tick:0000000000000000}";
                return timestamp[^16..];
            },
            PluginConfig.TimestampType.FrameCount => (le) =>
            {
                var context = le.AsLogEventWrapper();
                var timestamp = $"{context.Frame:0000000000000000}";
                return timestamp[^16..];
            },
            PluginConfig.TimestampType.Counter => (le) =>
            {
                var context = le.AsLogEventWrapper();
                var timestamp = $"{context.Uuid:0000000000000000}";
                return timestamp[^16..];
            },
            _ => throw new ArgumentOutOfRangeException(
                $"{PluginConfig.Timestamps.Type.Value} is not a valid TimestampType")
        };

        LoggerPatch.SyncListeners.Add(BepInEx.Preloader.Preloader.PreloaderLog);
        LoggerPatch.UnfilteredListeners.Add(BepInEx.Preloader.Preloader.PreloaderLog);

        FilterConfig.LevelMasks[Logger.InternalLogSource] = LogLevel.All;
        FilterConfig.LevelMasks[Log] = LogLevel.All;

        foreach (var source in Logger.Sources)
        {
            if (source is not HarmonyLogSource)
                continue;

            FilterConfig.LevelMasks[source] = LogLevel.All;
            break;
        }
    }

    public static void Finish()
    {
        XmlConfig.WriteLogConfig();

        SqliteLogger.Init(Path.Combine(Paths.BepInExRootPath, "LogOutput.sqlite"));

        _harmony = new Harmony(GUID);
        _harmony.PatchAll(typeof(PreloaderConsoleListenerPatch));
        _harmony.PatchAll(typeof(ChainloaderPatch));
        _harmony.PatchAll(typeof(LoggerPatch));

        Log.LogInfo($"{NAME}:{VERSION} Prepatcher Finished");
    }

    internal static Func<LogEventArgs, object> GetLogTimestamp;

    internal static void OnApplicationQuit()
    {
        _quitting = true;
        PluginConfig.CleanOrphanedEntries(PluginConfig.FilterConfig);

        foreach (var threadWrapper in ThreadWrapper.Wrappers)
        {
            threadWrapper?.Stop(PluginConfig.Scheduler.ShutdownType.Value == PluginConfig.ShutdownType.Instant);
        }

        SqliteLogger.Terminate(PluginConfig.Scheduler.ShutdownType.Value == PluginConfig.ShutdownType.Instant);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void VerboseLogWrappingLog(LogLevel level, [NotNull] Func<string> logline)
    {
        if ((level & PluginConfig.Debug.LogWrappingVerbosity.Value) != 0)
            Log.Log(level, logline());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void VerboseLog(LogLevel level, [NotNull] Func<string> logline)
    {
        //TODO: Add config for this logtype
        //if (PluginConfig.Debug.VerboseCecil.Value)
        Log.Log(level, logline());
    }

    [Obsolete("RegisterIgnoredILogListener is deprecated please use RegisterSyncListener instead")]
    public static void RegisterIgnoredILogListener(ILogListener toIgnore)
    {
        API.AsyncLoggersAPI.UpdateListenerFlags(toIgnore, API.AsyncLoggersAPI.LogListenerFlags.SyncHandling);
    }
}