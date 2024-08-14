using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BepInEx;
using SQLite;

namespace AsyncLoggers.DBAPI
{
    public static class SqliteChecker
    {
        internal static bool isLoaded()
        {
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

    internal static class SqliteLogger
    {

        internal static bool Enabled { get; set; }

        internal static SQLiteConnection Connection { get; private set; }
        internal static int ExecutionId { get; private set; }

        internal static void Init(string outputFile)
        {
            Enabled = PluginConfig.DbLogger.Enabled.Value && SqliteChecker.isLoaded();
            if (PluginConfig.DbLogger.Enabled.Value)
            {
                try
                {
                    if (SqliteChecker.isLoaded())
                    {
                        AsyncLoggers.Log.LogDebug($"creating db");
                        try
                        {
                            if (File.Exists(outputFile))
                            {
                                var filesize = new FileInfo(outputFile).Length;
                                AsyncLoggers.Log.LogDebug($"db existed and was {filesize} bytes");
                                if (filesize > PluginConfig.DbLogger.RotationSize.Value)
                                {
                                    AsyncLoggers.Log.LogWarning($"rotating db file");
                                    var rotationFile = outputFile + ".1";
                                    if (File.Exists(rotationFile))
                                        File.Delete(rotationFile);
                                    File.Move(outputFile, rotationFile);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            AsyncLoggers.Log.LogError($"Exception while rotating files! {ex}");
                            AsyncLoggers.Log.LogWarning($"Db defaulted to append mode");
                        }

                        Connection = new SQLiteConnection(outputFile);

                        Connection.CreateTable<Tables.Executions>(CreateFlags.AutoIncPK);
                        Connection.CreateTable<Tables.Mods>(CreateFlags.AutoIncPK);
                        Connection.CreateTable<Tables.ModData>(CreateFlags.AutoIncPK);

                        ExecutionId = GetExecution();

                        AsyncLoggers.Log.LogDebug($"ExecutionID is {ExecutionId}");
                    }
                    else
                    {
                        AsyncLoggers.Log.LogError($"No Sqlite dll found disabling Database!");
                    }
                }
                catch (Exception ex)
                {
                    AsyncLoggers.Log.LogError($"Exception while initializing the database! {ex}");
                    Enabled = false;
                }
            }
        }

        internal static void Terminate(bool immediate)
        {
            Enabled = false;
        }
        internal static void WriteMods(IEnumerable<PluginInfo> loadedMods)
        {
            if (Enabled)
            {
                Task.Factory.StartNew(() => _WriteMods(loadedMods));
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
                [PrimaryKey, AutoIncrement] public int _id { get; set; }
                public int execution_id { get; set; }
                public string modID { get; set; }
                public string modName { get; set; }
                public string modVersion { get; set; }
            }

            public class Events
            {
                [PrimaryKey, AutoIncrement] public int _id { get; set; }
                public int execution_id { get; set; }
                public int UUID { get; set; }
                public string timestamp { get; set; }
                public string source { get; set; }
                public string tag { get; set; }
                public string data { get; set; }
            }

            public class ModData
            {
                [PrimaryKey, AutoIncrement] public int _id { get; set; }
                public int execution_id { get; set; }
                public string timestamp { get; set; }
                public string source { get; set; }
                public string tag { get; set; }
                public string data { get; set; }
            }
        }
    }
}