using System;
using System.Threading;
using AsyncLoggers.Config;
using BepInEx.Logging;
using UnityEngine;

namespace AsyncLoggers.Wrappers.EventArgs;

public class ExtendedLogEventArgs : LogEventArgs
{
    private static long _logCounter = 0L;

    public static ExtendedLogEventArgs CreateNewHolder(LogEventArgs eventArgs, string stacktrace = null)
    {
        var uuid = (ulong)Interlocked.Increment(ref _logCounter);
        var timestamp = DateTime.UtcNow;
        var tick = (uint)Environment.TickCount;
        var thread = Thread.CurrentThread;
        var threadId = thread.ManagedThreadId;
        var threadName = thread.Name;

        int? frame = null;
        if (threadId == AsyncLoggers.MainThreadID && !AsyncLoggers._quitting)
            frame = Time.frameCount;

        var isFiltered = CheckFilter(eventArgs);

        return new ExtendedLogEventArgs(eventArgs, uuid, timestamp, frame, tick, threadId, threadName, stacktrace, isFiltered);
    }

    private static bool CheckFilter(LogEventArgs eventArgs)
    {
        var sourceMask = FilterConfig.GetMaskForSource(eventArgs.Source);
        return (eventArgs.Level & sourceMask) == 0;
    }

    public readonly ulong Uuid;

    public readonly DateTime Timestamp;

    public readonly int? Frame;
    public readonly uint Tick;

    public readonly int ThreadID;

    public readonly string ThreadName;

    public readonly string StackTrace;

    public readonly bool IsFiltered;

    protected ExtendedLogEventArgs(LogEventArgs eventArgs, ulong uuid, DateTime timestamp, int? frame, uint tick, int threadID, string threadName, string stackTrace, bool isFiltered) : base(eventArgs.Data, eventArgs.Level, eventArgs.Source)
    {
        Uuid = uuid;
        Timestamp = timestamp;
        Frame = frame;
        Tick = tick;
        ThreadID = threadID;
        ThreadName = threadName;
        StackTrace = stackTrace;
        IsFiltered = isFiltered;
    }

    internal ExtendedLogEventArgs ToTimestampedEventArgs()
    {

        if (!PluginConfig.Timestamps.Enabled.Value)
            return this;

        if (this is TimestampedLogEventArg)
            return this;

        var timestamp = PluginConfig.Timestamps.Type.Value switch
        {
            PluginConfig.TimestampType.DateTime => Timestamp.ToString("HH:mm:ss.fffffff"),
            PluginConfig.TimestampType.TickCount => Tick.ToString("D16"),
            PluginConfig.TimestampType.FrameCount => (Frame?.ToString("D16") ?? FitString(ThreadName ?? "T"  + ThreadID.ToString("D5"),16)),
            PluginConfig.TimestampType.Counter => Uuid.ToString("D16"),
            _ => throw new ArgumentOutOfRangeException(
                $"{PluginConfig.Timestamps.Type.Value} is not a valid TimestampType")
        };

        return new TimestampedLogEventArg(this, $"[{timestamp}] ");

        string FitString(string input, int length)
        {
            if (string.IsNullOrEmpty(input))
                input = "";

            // Truncate if too long
            if (input.Length > length)
                return input.Substring(0, length);

            // If shorter, pad and center
            int totalPadding = length - input.Length;
            int padLeft = totalPadding / 2 + input.Length;
            return input.PadLeft(padLeft).PadRight(length);
        }

    }
}
