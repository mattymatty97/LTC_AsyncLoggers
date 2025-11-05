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
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using Mono.Cecil;
using MonoMod.RuntimeDetour;
using Logger = BepInEx.Logging.Logger;
// ReSharper disable CollectionNeverQueried.Global

namespace AsyncLoggers;

public static class AsyncLoggers
{
    public const string GUID = MyPluginInfo.PLUGIN_GUID;
    public const string NAME = MyPluginInfo.PLUGIN_NAME;
    public const string VERSION = MyPluginInfo.PLUGIN_VERSION;

    internal static int MainThreadID;

    internal static readonly BepInPlugin Plugin = new BepInPlugin(GUID, NAME, VERSION);
    internal static ManualLogSource Log { get; } = Logger.CreateLogSource(nameof(AsyncLoggers));
    internal static ManualLogSource EmergencyLog { get; } = Logger.CreateLogSource($"E-{nameof(AsyncLoggers)}");
    internal static ManualLogSource WrappedUnitySource { get; } = Logger.CreateLogSource("Unity Log");

    internal static Harmony Harmony = new Harmony(GUID);

    internal static readonly List<Hook> Hooks = [];

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

    // Cannot be renamed, method name is important
    public static void Initialize()
    {
        Log.LogInfo($"{NAME}:{VERSION} Prepatcher Started");

        MainThreadID = Thread.CurrentThread.ManagedThreadId;

        PluginConfig.Init();

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

        ChainloaderPatch.InitMonoMod();

        Log.LogInfo($"{NAME}:{VERSION} Prepatcher Finished");
    }

    internal static bool _quitting;

    internal static void OnApplicationQuit()
    {
        _quitting = true;
        //PluginConfig.CleanOrphanedEntries(PluginConfig.FilterConfig);

        foreach (var threadWrapper in ThreadWrapper.Wrappers)
        {
            threadWrapper?.Stop(PluginConfig.Scheduler.ShutdownType.Value == PluginConfig.ShutdownType.Instant);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VerboseLogWrappingLog(LogLevel level, Func<string> logline)
    {
        if ((level & PluginConfig.Debug.LogWrappingVerbosity.Value) != 0)
            Log.Log(level, logline());
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VerboseSqliteLog(LogLevel level, Func<string> logline)
    {
        if ((level & PluginConfig.Debug.SqliteVerbosity.Value) != 0)
            Log.Log(level, logline());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [UsedImplicitly]
    internal static void VerboseLog(LogLevel level, Func<string> logline)
    {
        //TODO: Add config for this logtype
        //if (PluginConfig.Debug.VerboseCecil.Value)
        Log.Log(level, logline());
    }

    [Obsolete("RegisterIgnoredILogListener is deprecated please use RegisterSyncListener instead")]
    [UsedImplicitly]
    public static void RegisterIgnoredILogListener(ILogListener toIgnore)
    {
        API.AsyncLoggersAPI.UpdateListenerFlags(toIgnore, API.AsyncLoggersAPI.LogListenerFlags.SyncHandling);
    }
}
