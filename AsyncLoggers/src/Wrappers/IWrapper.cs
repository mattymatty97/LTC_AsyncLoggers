using System;

namespace AsyncLoggers.Wrappers;

public interface IWrapper
{
    public event Action OnBecomeIdle;
    public event Action Stopping;
    
    public delegate void LogCallback(object sender, BepInEx.Logging.LogEventArgs eventArgs);

    public void Schedule(LogCallback callback, object sender, BepInEx.Logging.LogEventArgs eventArgs);

    public void Stop(bool immediate = false);
}