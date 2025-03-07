using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
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
    public const string VERSION = "2.1.3";

    internal static Thread MainThread;

    internal static readonly BepInPlugin Plugin = new BepInPlugin(GUID, NAME, VERSION);
    internal static ManualLogSource Log { get; } = Logger.CreateLogSource(nameof(AsyncLoggers));
    internal static ManualLogSource WrappedUnitySource { get; } = Logger.CreateLogSource("Unity Log");

    internal static Harmony Harmony;

    internal static int StartTime { get; private set; }

    public static IEnumerable<string> TargetDLLs => GetDLLs();

    private static IEnumerable<string> GetDLLs()
    {
        if (!PluginConfig.LogWrapping.Enabled.Value)
            yield break;

        var dlls = Utility.GetUniqueFilesInDirectories(BepInEx.Paths.DllSearchPaths, "*.dll");

        var assemblies = PluginConfig.LogWrapping.TargetGameAssemblies.Value.Split(",").Select(p => p.Trim()).ToList();
        
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
            Log.LogWarning($"Parsing {assembly.Name.Name} for Unity.Debug calls!");
            AssemblyAnalyzer.ProcessAssembly(assembly, out var count);
            Log.LogInfo($"Found {count} Unity.Debug calls in {assembly.Name.Name}");
        }
        catch (Exception ex)
        {
            Log.LogFatal(ex);
        }
    }

    private static int _lastUnityFrame = 0;
    private static bool _quitting = false;

    public static int UnityFrameCount
    {
        get
        {
            int frame;
            if (Thread.CurrentThread.ManagedThreadId != MainThread?.ManagedThreadId)
            {
                return -1;
            }
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

        StartTime = Environment.TickCount & Int32.MaxValue;
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

        Harmony = new Harmony(GUID);
        Harmony.PatchAll(typeof(PreloaderConsoleListenerPatch));
        Harmony.PatchAll(typeof(ChainloaderPatch));
        Harmony.PatchAll(typeof(LoggerPatch));

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
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VerboseLogWrappingLog(LogLevel level, [NotNull] Func<string> logline)
    {
        if ((level & PluginConfig.Debug.LogWrappingVerbosity.Value) != 0)
            Log.Log(level, logline());
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VerboseSqliteLog(LogLevel level, [NotNull] Func<string> logline)
    {
        if ((level & PluginConfig.Debug.SqliteVerbosity.Value) != 0)
            Log.Log(level, logline());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
