using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using AsyncLoggers.StaticContexts;
using AsyncLoggers.Wrappers;
using BepInEx;
using SQLite;

namespace AsyncLoggers.DBAPI
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
    
    internal static class SqliteLoggerImpl
    {
        private static IWrapper asyncScheduler;

        internal static bool Enabled { get; private set; }

        internal static SQLiteConnection Connection { get; private set; }
        internal static int ExecutionId { get; private set; }

        internal static void Init(string outputFile)
        {
            Enabled = PluginConfig.DbLogger.Enabled.Value && SqliteChecker.isLoaded();
            if (Enabled)
            {
                AsyncLoggerPreloader.Log.LogDebug($"creating db");
                if (File.Exists(outputFile))
                {
                    var filesize = new FileInfo(outputFile).Length;
                    AsyncLoggerPreloader.Log.LogDebug($"db existed and was {filesize} bytes");
                    if (filesize > PluginConfig.DbLogger.RotationSize.Value)
                    {
                        AsyncLoggerPreloader.Log.LogWarning($"rotating db file");
                        File.Move(outputFile, outputFile + ".1");
                    }
                }

                Connection = new SQLiteConnection(outputFile);

                Connection.CreateTable<Tables.Executions>(CreateFlags.AutoIncPK);
                Connection.CreateTable<Tables.Mods>(CreateFlags.AutoIncPK);
                Connection.CreateTable<Tables.Events>(CreateFlags.AutoIncPK);

                ExecutionId = GetExecution();

                AsyncLoggerPreloader.Log.LogDebug($"ExecutionID is {ExecutionId}");

                Connection.Insert(new Tables.Events
                {
                    execution_id = ExecutionId,
                    UUID = (int)EventContext.Uuid!,
                    timestamp = GenericContext.FormatTimestamp(Process.GetCurrentProcess().StartTime.ToUniversalTime()),
                    source = "Application",
                    tag = "Start",
                    data = "Process Started"
                });
                asyncScheduler = new ThreadWrapper(IWrapper.ContextType.Event);
            }
            else
            {
                AsyncLoggerPreloader.Log.LogError($"No Sqlite dll found disabling Database!");
            }
        }

        internal static void Terminate(bool immediate)
        {
            Enabled = false;
            asyncScheduler.Stop(immediate);
        }

        internal static void WriteEvent(string source, string tag, string data, DateTime? timestamp = null)
        {
            if (Enabled)
            {
                asyncScheduler.Schedule(() =>
                {
                    _WriteEvent(source, tag, data, timestamp);
                });
            }
        }
        
        private static void _WriteEvent(string source, string tag, string data, DateTime? timestamp = null)
        {
            timestamp ??= GenericContext.Timestamp;
            var value = new Tables.Events
            {
                execution_id = ExecutionId,
                UUID = (int)EventContext.Uuid!,
                timestamp = GenericContext.FormatTimestamp(timestamp),
                source = source,
                tag = tag,
                data = data
            };
            Connection.Insert(value);
            
            Task.Factory.StartNew(()=>SqliteLogger.onEventWritten(value.UUID,value.source,value.tag,value.data,timestamp!.Value));
        }
        
        internal static void WriteData(string source, string tag, string data, DateTime? timestamp = null)
        {
            if (Enabled)
            {
                EventContext.Uuid = 0;
                asyncScheduler.Schedule(() =>
                {
                    _WriteData(source, tag, data, timestamp);
                });
                EventContext.Uuid = null;
            }
        }
        
        private static void _WriteData(string source, string tag, string data, DateTime? timestamp = null)
        {
            timestamp ??= GenericContext.Timestamp;

            var value = new Tables.ModData()
            {
                execution_id = ExecutionId,
                timestamp = GenericContext.FormatTimestamp(timestamp),
                source = source,
                tag = tag,
                data = data
            };
            
            Connection.Insert(value);
            
            Task.Factory.StartNew(()=>SqliteLogger.onDataWritten(value.source,value.tag,value.data,timestamp!.Value));

        }
        
        internal static void WriteMods(IEnumerable<PluginInfo> loadedMods)
        {
            if (Enabled)
            {
                Task.Factory.StartNew(()=>_WriteMods(loadedMods));
            }
        }
        
        private static void _WriteMods(IEnumerable<PluginInfo> loadedMods)
        {
            foreach (var pluginInfo in loadedMods)
            {
                Connection.Insert(new Tables.Mods
                {
                    execution_id = ExecutionId,
                    modID = pluginInfo.Metadata.GUID,
                    modName = pluginInfo.Metadata.Name,
                    modVersion = pluginInfo.Metadata.Version.ToString(),
                });
            }
        }

        private static int GetExecution()
        {
            var execution = new Tables.Executions();
            Connection.Insert(execution);
            return execution._id;
        }

        private static class Tables
        {
            public class Executions
            {
                [PrimaryKey, AutoIncrement] public int _id { get; set; }
            }
            
            public class Mods
            {
                [PrimaryKey, AutoIncrement] 
                public int _id { get; set; }
                public int execution_id { get; set; }
                public string modID { get; set; }
                public string modName { get; set; }
                public string modVersion { get; set; }
            }
            
            public class Events
            {
                [PrimaryKey, AutoIncrement] 
                public int _id { get; set; }
                public int execution_id { get; set; }
                public int UUID { get; set; }
                public string timestamp { get; set; }
                public string source { get; set; }
                public string tag { get; set; }
                public string data { get; set; }
            }            
            
            public class ModData
            {
                [PrimaryKey, AutoIncrement] 
                public int _id { get; set; }
                public int execution_id { get; set; }
                public string timestamp { get; set; }
                public string source { get; set; }
                public string tag { get; set; }
                public string data { get; set; }
            }
        }
    }
}