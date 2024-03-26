using System;

namespace AsyncLoggers.DBAPI
{
    public static class SqliteLogger
    {
        public delegate void EventListener(int uuid, string source, string tag, string data, DateTime timestamp);
        public delegate void DataListener(string source, string tag, string data, DateTime timestamp);
        
        public static bool Enabled => SqliteLoggerImpl.Enabled;
        
        public static int ExecutionId => SqliteLoggerImpl.ExecutionId;

        public static event EventListener onEvent;
        public static event DataListener onData;

        public static void WriteEvent(string source, string tag, string data, DateTime? timestamp = null)
        {
            SqliteLoggerImpl.WriteEvent(source, tag, data, timestamp);
        }
        
        public static void WriteData(string source, string tag, string data, DateTime? timestamp = null)
        {
            SqliteLoggerImpl.WriteData(source, tag, data, timestamp);
        }

        internal static void onEventWritten(int uuid, string source, string tag, string data, DateTime timestamp)
        {
            onEvent.Invoke(uuid, source, tag, data, timestamp);
        }
        
        internal static void onDataWritten(string source, string tag, string data, DateTime timestamp)
        {
            onData.Invoke(source, tag, data, timestamp);
        }
    }
}