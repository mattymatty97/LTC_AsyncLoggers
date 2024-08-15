namespace AsyncLoggers.Wrappers.LogEventArgs;

public class TimestampedLogEventArg(LogEventWrapper original)
    : LogEventWrapper(original.Data, original.Level, original.Source, original.Timestamp, original.AppTimestamp, original.Uuid, original.StackTrace)
{
    public override string ToString()
    {
        return $"[{AppTimestamp}] {base.ToString()}";
    }
}