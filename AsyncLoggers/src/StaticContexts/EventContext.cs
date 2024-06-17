using System;
using System.Threading;

namespace AsyncLoggers.StaticContexts
{
    public static class EventContext
    {
        [ThreadStatic]
        private static long? _uuid;

        private static long _eventCounter = 0L;

        public static long? Uuid
        {
            get => _uuid ?? Interlocked.Increment(ref _eventCounter);
            set => _uuid = value;
        }
    }
}