namespace AsyncLoggers.Wrappers.LogEventArgs;

public class TimestampedLogEventArg(LogEventWrapper original)
    : LogEventWrapper(original.Data, original.Level, original.Source, original.StackTrace)
{
    public override string ToString()
    {
        return $"[{AsyncLoggers.GetLogTimestamp(this)}] {base.ToString()}";
    }
}