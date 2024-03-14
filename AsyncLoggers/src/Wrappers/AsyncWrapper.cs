using System;
using System.Threading;
using DisruptorUnity3d;

namespace AsyncLoggers.Wrappers
{
    public interface IAsyncWrapper
    {
        public delegate void LogCallback();

        public void Schedule(LogCallback callback);

        public void Stop(bool immediate = false);
    }
}