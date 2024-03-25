using System;
using System.IO;
using System.Threading.Tasks;
using BepInEx.Logging;
using SQLite;
using LogEventArgs = BepInEx.Logging.LogEventArgs;
#pragma warning disable CS0169 // Field is never used

namespace AsyncLoggers.BepInExListeners
{
   
    public class AsyncSqliteListener : ILogListener
    {

        private readonly string _filePath;
        private readonly SQLiteAsyncConnection connection;

        private readonly int execution_id;

        public AsyncSqliteListener(string outputFile)
        {
            AsyncLoggerPreloader.Log.LogDebug($"creating db");
            if (File.Exists(outputFile))
            {
                var filesize = new FileInfo(outputFile).Length;
                if (filesize > PluginConfig.DbLogger.RotationSize.Value)
                    File.Move(outputFile, outputFile + ".1");
                AsyncLoggerPreloader.Log.LogDebug($"db existed and was {filesize} bytes");
            }
            
            connection = new SQLiteAsyncConnection(outputFile);
            AsyncLoggerPreloader.Log.LogDebug($"creating db");
            InitDb(connection);
            execution_id = GetExecution(connection);
            AsyncLoggerPreloader.Log.LogDebug($"ExecutionID is {execution_id}");
        }

        public void Dispose()
        {
        }

        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            var log = new SqliteListener.Tables.Logs
            {
                execution_id = execution_id,
                UUID = (int)LogContext.Uuid!,
                timestamp = LogContext.Timestamp?.ToString("MM/dd/yyyy HH:mm:ss.fffffff"),
                source = eventArgs.Source.SourceName,
                level = eventArgs.Level.ToString(),
                message = eventArgs.Data?.ToString()
            };
            connection.InsertAsync(log);
        }

        private static int GetExecution(SQLiteAsyncConnection connection)
        {
            var execution = new SqliteListener.Tables.Executions();
            connection.InsertAsync(execution).Wait();
            return execution._id;
        }

        private static void InitDb(SQLiteAsyncConnection connection)
        {
            Task[] tasks = {
                connection.CreateTableAsync<SqliteListener.Tables.Logs>(CreateFlags.AutoIncPK),
                connection.CreateTableAsync<SqliteListener.Tables.Executions>(CreateFlags.AutoIncPK)
            };
            Task.WaitAll(tasks);
        }
    }
}