using System;
using System.Threading;

namespace AsyncLoggers.StaticContexts
{
    public static class EventContext
    {
        [ThreadStatic]
        private static long? _UUID;

        private static long logCounter = 0L;

        public static long? Uuid
        {
            get => _UUID ?? Interlocked.Increment(ref logCounter);
            set => _UUID = value;
        }
    }
}