using AsyncLoggers.DBAPI;
using AsyncLoggers.StaticContexts;
using BepInEx.Logging;
using SQLite;
using LogEventArgs = BepInEx.Logging.LogEventArgs;
#pragma warning disable CS0169 // Field is never used

namespace AsyncLoggers.BepInExListeners
{
    
    public class SqliteListener : ILogListener
    {
        public SqliteListener()
        {
            SqliteLoggerImpl.Connection.CreateTable<Tables.Logs>(CreateFlags.AutoIncPK);
        }

        public void Dispose()
        {
        }

        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            var log = new Tables.Logs
            {
                execution_id = SqliteLoggerImpl.ExecutionId,
                UUID = (int)LogContext.Uuid!,
                timestamp = GenericContext.FormatTimestamp(GenericContext.Timestamp),
                source = eventArgs.Source.SourceName,
                level = eventArgs.Level.ToString(),
                message = eventArgs.Data?.ToString()
            };
            SqliteLoggerImpl.Connection.Insert(log);
        }
        
        internal static class Tables
        {
            public class Logs
            {
                [PrimaryKey, AutoIncrement] public int _id { get; set; }
                [Indexed] public int execution_id { get; set; }
                [Indexed] public int UUID { get; set; }
                [Indexed] public string timestamp { get; set; }
                [Indexed] public string source { get; set; }
                [Indexed] public string level { get; set; }
                public string message { get; set; }
            }
        }
    }
}