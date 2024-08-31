namespace AsyncLoggers.Wrappers.EventArgs;

public class TimestampedLogEventArg : LogEventWrapper
{
    internal TimestampedLogEventArg(LogEventWrapper original) : base(original.Data, original.Level, original.Source, original.Frame, original.Tick, original.Timestamp, original.AppTimestamp, original.Uuid, original.StackTrace)
    {
    }

    public override string ToString()
    {
        return $"[{AppTimestamp}] {base.ToString()}";
    }
}