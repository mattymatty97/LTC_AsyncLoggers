using System;
using System.Collections.Concurrent;
using AsyncLoggers.Wrappers;
using BepInEx.Logging;

namespace AsyncLoggers.StaticContexts
{
    public class GenericContext
    {
        internal static bool PreChainloader { get; set; } = true;
        
        internal static readonly ConcurrentDictionary<ILogListener, IWrapper> _wrappersMap =
            new ConcurrentDictionary<ILogListener, IWrapper>();
        
        [ThreadStatic]
        private static bool _async;

        [ThreadStatic]
        private static DateTime? _timestamp;

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

        internal static string FormatTimestamp(DateTime? timestamp)
        {
            return timestamp?.ToString("MM/dd/yyyy HH:mm:ss.fffffff");
        }

    }
}