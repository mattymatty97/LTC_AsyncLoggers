using System;
using System.Diagnostics;
using System.Threading;

namespace AsyncLoggers.StaticContexts
{
    public static class LogContext
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