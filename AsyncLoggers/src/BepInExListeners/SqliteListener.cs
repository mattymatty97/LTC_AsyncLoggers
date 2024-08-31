using System;
using System.Globalization;
using AsyncLoggers.Wrappers;
using AsyncLoggers.Wrappers.EventArgs;
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
            SqliteLogger.Connection.CreateTable<Tables.Logs>(CreateFlags.AutoIncPK);
        }

        public void Dispose()
        {
        }

        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            var context = eventArgs.AsLogEventWrapper();
            var log = new Tables.Logs
            {
                execution_id = SqliteLogger.ExecutionId,
                UUID = (int)context.Uuid,
                timestamp = context.Timestamp.ToString("o", CultureInfo.InvariantCulture),
                tickCount = context.Tick,
                frameCount = context.Frame,
                source = context.Source.SourceName,
                level = context.Level.ToString(),
                message = context.Data?.ToString(),
                stacktrace = context.StackTrace
            };
            try
            {
                SqliteLogger.Connection.Insert(log);
            }
            catch (Exception ex)
            {
                AsyncLoggers.Log.LogError(
                    $"Exception writing log to db [{log.level} {log.source}]: {ex}");
                SqliteLogger.Enabled = false;
            }
        }
        
        internal static class Tables
        {
            public class Logs
            {
                [PrimaryKey, AutoIncrement] public int _id { get; set; }
                [Indexed] public int execution_id { get; set; }
                [Indexed] public int UUID { get; set; }
                [Indexed] public string timestamp { get; set; }
                
                [Indexed] public int tickCount { get; set; }
                
                [Indexed] public int frameCount { get; set; }
                [Indexed] public string source { get; set; }
                [Indexed] public string level { get; set; }
                public string message { get; set; }
                
                public string stacktrace { get; set; }
            }
        }
    }
}