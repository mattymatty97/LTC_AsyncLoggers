using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using AsyncLoggers.BepInExListeners;
using AsyncLoggers.Cecil;
using AsyncLoggers.Patches;
using AsyncLoggers.Wrappers;
using AsyncLoggers.Wrappers.LogEventArgs;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Mono.Cecil;
using Logger = BepInEx.Logging.Logger;

namespace AsyncLoggers;

public static class AsyncLoggers
{
    public const string GUID = "mattymatty.AsyncLoggers";
    public const string NAME = "AsyncLoggers";
    public const string VERSION = "2.0.0";
    internal static ManualLogSource Log { get; } = Logger.CreateLogSource(nameof(AsyncLoggers));
    internal static ManualLogSource WrappedUnitySource { get; } = Logger.CreateLogSource("Unity Log");

    private static Harmony _harmony;
        
    private static int _startTime;

    public static IEnumerable<string> TargetDLLs => GetDLLs();

    private static IEnumerable<string> GetDLLs()
    {
        if (!PluginConfig.LogWrapping.Enabled.Value)
            yield break;
        
        var assemblies = PluginConfig.LogWrapping.TargetAssemblies.Value.Split(",");
        foreach (var assembly in assemblies)
        {
            yield return Path.GetFileName(assembly);
        }
    }

    public static void Patch(AssemblyDefinition assembly)
    {
        try
        {
            AsyncLoggers.Log.LogWarning($"Parsing {assembly.Name.Name} for Unity.Debug calls!");
            WrapDebugs.ProcessAssembly(assembly);
        }
        catch (Exception ex)
        {
            AsyncLoggers.Log.LogFatal(ex);
        }
    }

    // Cannot be renamed, method name is important
    public static void Initialize()
    {
        Log.LogInfo($"{NAME} Prepatcher Started");
        PluginConfig.Init();

        _startTime = Environment.TickCount & Int32.MaxValue;
        if (PluginConfig.Timestamps.Enabled.Value)
            Log.LogWarning(
                $"{NAME} Timestamps start at {DateTime.UtcNow:dddd, dd MMMM yyyy HH:mm:ss.fffffff} UTC");

        GetLogTimestamp = PluginConfig.Timestamps.Type.Value switch
        {
            PluginConfig.TimestampType.DateTime => (le) =>
            {
                var context = le as LogEventWrapper;
                var timestamp = context?.Timestamp ?? DateTime.Now;
                return timestamp.ToString("HH:mm:ss.fffffff");
            },
            PluginConfig.TimestampType.TickCount => (_) =>
            {
                var timestamp = $"{(Environment.TickCount & Int32.MaxValue) - _startTime:0000000000000000}";
                return timestamp.Substring(timestamp.Length - 16);
            },
            PluginConfig.TimestampType.Counter => (le) =>
            {
                var context = le as LogEventWrapper;
                var timestamp = $"{context?.Uuid ?? -1:0000000000000000}";
                return timestamp.Substring(timestamp.Length - 16);
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
            if (source is not  HarmonyLogSource)
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
        Log.LogInfo($"{NAME} Prepatcher Finished");
    }

    internal static Func<LogEventArgs, object> GetLogTimestamp;

    internal static void OnApplicationQuit()
    {
        PluginConfig.CleanOrphanedEntries(PluginConfig.FilterConfig);
            
        foreach (var threadWrapper in ThreadWrapper.Wrappers)
        {
            threadWrapper?.Stop(PluginConfig.Scheduler.ShutdownType.Value == PluginConfig.ShutdownType.Instant);
        }

        SqliteLogger.Terminate(PluginConfig.Scheduler.ShutdownType.Value == PluginConfig.ShutdownType.Instant);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void VerboseCecilLog(LogLevel level, string logline)
    {
        if (PluginConfig.Debug.VerboseCecil.Value)
            Log.Log(level, logline);
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void VerboseLog(LogLevel level, string logline)
    {
        //TODO: Add config for this logtype
        //if (PluginConfig.Debug.VerboseCecil.Value)
            Log.Log(level, logline);
    }

    [Obsolete("RegisterIgnoredILogListener is deprecated please use RegisterSyncListener instead")]
    public static void RegisterIgnoredILogListener(ILogListener toIgnore)
    {
        RegisterSyncListener(toIgnore);
    }
        
    /**
     * Mark this listener as forcefully sync ( will not be deferred )
     */
    public static bool RegisterSyncListener(ILogListener listener)
    {
        return LoggerPatch.SyncListeners.Add(listener);
    }
        
    /**
     * Remove this listener from the list ( mainly for GC purposes )
     */
    public static bool UnRegisterSyncListener(ILogListener listener)
    {
        return LoggerPatch.SyncListeners.Remove(listener);
    }
        
    /**
     * Mark this listener as not affected by the source filters ( will ignore AsyncLoggers.Filter.cfg )
     */
    public static bool RegisterUnfilteredListener(ILogListener listener)
    {
        return LoggerPatch.UnfilteredListeners.Add(listener);
    }
        
    /**
     * Remove this listener from the list ( mainly for GC purposes )
     */
    public static bool UnRegisterUnfilteredListener(ILogListener listener)
    {
        return LoggerPatch.UnfilteredListeners.Remove(listener);
    }
        
    /**
     * Mark this listener as Timestamped ( will prepend the text with [{Timestamp}] )
     */
    public static bool RegisterTimestampedListener(ILogListener listener)
    {
        return LoggerPatch.TimestampedListeners.Add(listener);
    }
        
    /**
     * Remove this listener from the list ( mainly for GC purposes )
     */
    public static bool UnRegisterTimestampedListener(ILogListener listener)
    {
        return LoggerPatch.TimestampedListeners.Remove(listener);
    }

    /**
     * Which levels will collect and store a stacktrace
     */
    public static LogLevel TraceableLevelsMaks = LogLevel.Error | LogLevel.Fatal;
}