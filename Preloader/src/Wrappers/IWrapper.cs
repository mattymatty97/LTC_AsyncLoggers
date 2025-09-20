using System;
using AsyncLoggers.Wrappers.EventArgs;

namespace AsyncLoggers.Wrappers;

public interface IWrapper
{
    public event Action OnBecomeIdle;
    public event Action Stopping;
    
    public delegate void LogCallback(object sender, ExtendedLogEventArgs eventArgs);

    public void Schedule(LogCallback callback, object sender, ExtendedLogEventArgs eventArgs);

    public void Stop(bool immediate = false);
}
