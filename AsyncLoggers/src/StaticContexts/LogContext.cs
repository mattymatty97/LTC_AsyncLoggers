using System;
using System.Diagnostics;
using System.Threading;

namespace AsyncLoggers.StaticContexts
{
    public static class LogContext
    {
        [ThreadStatic]
        private static long? _uuid;

        private static long _logCounter = 0L;

        public static long? Uuid
        {
            get => _uuid ?? (GenericContext.PreChainloader?0L:Interlocked.Increment(ref _logCounter));
            set => _uuid = value;
        }
    }
}