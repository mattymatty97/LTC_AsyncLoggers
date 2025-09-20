namespace AsyncLoggers.Wrappers.EventArgs;

public class TimestampedLogEventArg : ExtendedLogEventArgs
{
    private readonly string TimeStamp;
    internal TimestampedLogEventArg(ExtendedLogEventArgs original, string timestamp) :
        base(original, original.Uuid, original.Timestamp, original.Frame, original.Tick, original.ThreadID, original.ThreadName, original.StackTrace, original.IsFiltered)
    {
        TimeStamp = timestamp;
    }

    public override string ToString()
    {
        return $"{TimeStamp}{base.ToString()}";
    }
}
