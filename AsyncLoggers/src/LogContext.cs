using System;
using System.Diagnostics;
using System.Threading;

namespace AsyncLoggers
{
    public static class LogContext
    {
        [ThreadStatic]
        private static bool _async;
        
        [ThreadStatic]
        private static DateTime? _timestamp;

        [ThreadStatic]
        private static StackTrace _stacktrace;

        [ThreadStatic]
        private static long? _UUID;
        
        internal static long logCounter = 0L;


        public static bool Async
        {
            get => _async;
            set => _async = value;
        }

        public static DateTime? Timestamp
        {
            get
            {
                if (_timestamp != null)
                    return _timestamp;
                return DateTime.UtcNow;
            }
            set => _timestamp = value;
        }

        public static StackTrace Stacktrace
        {
            get => _stacktrace;
            set => _stacktrace = value;
        }

        public static long? Uuid
        {
            get
            {
                if (_UUID != null)
                    return _UUID;
                return Interlocked.Increment(ref logCounter);
            }
            set => _UUID = value;
        }
    }
}