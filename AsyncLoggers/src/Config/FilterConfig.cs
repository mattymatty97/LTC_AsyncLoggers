using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace AsyncLoggers.Config;

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