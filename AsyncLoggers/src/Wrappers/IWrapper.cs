namespace AsyncLoggers.Wrappers;

public interface IWrapper
{
    public delegate void LogCallback(object sender, BepInEx.Logging.LogEventArgs eventArgs);

    public void Schedule(LogCallback callback, object sender, BepInEx.Logging.LogEventArgs eventArgs);

    public void Stop(bool immediate = false);
}