namespace AsyncLoggers.Wrappers
{
    public interface IWrapper
    {
        public delegate void LogCallback();

        public void Schedule(LogCallback callback);

        public void Stop(bool immediate = false);
    }
}