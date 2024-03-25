using System;
using System.IO;
using System.Threading.Tasks;
using BepInEx.Logging;
using SQLite;
using LogEventArgs = BepInEx.Logging.LogEventArgs;
#pragma warning disable CS0169 // Field is never used

namespace AsyncLoggers.BepInExListeners
{
    public static class SqliteChecker
    {
        internal static bool isLoaded() {
            try
            {
                SQLite3.LibVersionNumber();
                return true;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
        }
    }
    
    public class SqliteListener : ILogListener
    {
        
        private readonly string _filePath;
        private readonly SQLiteConnection connection;

        private readonly int execution_id;

        public SqliteListener(string outputFile)
        {
            AsyncLoggerPreloader.Log.LogDebug($"creating db");
            if (File.Exists(outputFile))
            {
                var filesize = new FileInfo(outputFile).Length;
                if (filesize > PluginConfig.DbLogger.RotationSize.Value)
                    File.Move(outputFile, outputFile + ".1");
                AsyncLoggerPreloader.Log.LogDebug($"db existed and was {filesize} bytes");
            }
            
            connection = new SQLiteConnection(outputFile);
            AsyncLoggerPreloader.Log.LogDebug($"creating db");
            InitDb(connection);
            execution_id = GetExecution(connection);

            AsyncLoggerPreloader.Log.LogDebug($"ExecutionID is {execution_id}");
        }

        public void Dispose()
        {
            connection.Dispose();
        }

        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            var log = new Tables.Logs
            {
                execution_id = execution_id,
                UUID = (int)LogContext.Uuid!,
                timestamp = LogContext.Timestamp?.ToString("MM/dd/yyyy HH:mm:ss.fffffff"),
                source = eventArgs.Source.SourceName,
                level = eventArgs.Level.ToString(),
                message = eventArgs.Data?.ToString()
            };
            connection.Insert(log);
        }

        private static int GetExecution(SQLiteConnection connection)
        {
            var execution = new Tables.Executions();
            connection.Insert(execution);
            return execution._id;
        }

        private static void InitDb(SQLiteConnection connection)
        {
            connection.CreateTable<Tables.Logs>(CreateFlags.AutoIncPK);
            connection.CreateTable<Tables.Executions>(CreateFlags.AutoIncPK);
        }

        internal static class Tables
        {
            public class Executions
            {
                [PrimaryKey, AutoIncrement] public int _id { get; set; }
            }
            
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