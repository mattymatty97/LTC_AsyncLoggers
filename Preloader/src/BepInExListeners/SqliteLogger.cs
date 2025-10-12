using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AsyncLoggers.Config;
using AsyncLoggers.Proxy.WinAPI;
using AsyncLoggers.Sqlite;
using BepInEx;
using SQLite;

namespace AsyncLoggers.BepInExListeners
{
    
    internal static class SqliteLogger
    {
        private static Kernel32.NativeLibrary _sqliteLibrary;

        internal static bool Enabled { get; set; }

        internal static SQLiteConnection Connection { get; private set; }
        internal static int ExecutionId { get; private set; }

        internal static void Init(string outputFile)
        {
            if (!PluginConfig.DbLogger.Enabled.Value)
            {
                Enabled = false;
                return;
            }

            try
            {
                _sqliteLibrary = SQLiteLoader.EnsureSqliteLibrary();
                
                InitializeCallbacks();
            }
            catch (Exception ex)
            {
                AsyncLoggers.Log.LogError($"Exception initializing SQLite library:\n{ex}");
            }

            if (_sqliteLibrary == null)
            {
                if (PluginConfig.DbLogger.Enabled.Value)
                    AsyncLoggers.Log.LogError("Could not load SQLite library! DBLogger will be forcefully disabled!");
                Enabled = false;
                return;
            }
            
            Enabled = true;
            try
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
						
                Connection.ExecuteScalar<int>("PRAGMA journal_mode=WAL;");

                Connection.CreateTable<Tables.Executions>(CreateFlags.AutoIncPK);
                Connection.CreateTable<Tables.Mods>(CreateFlags.AutoIncPK);

                ExecutionId = GetExecution();

                AsyncLoggers.Log.LogDebug($"ExecutionID is {ExecutionId}");
            }
            catch (Exception ex)
            {
                AsyncLoggers.Log.LogError($"Exception while initializing the database! {ex}");
                Enabled = false;
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
        }
        
        private static void InitializeCallbacks()
        {
            var callbacks = new SQLite3.Callbacks
            {
                Threadsafe = _sqliteLibrary.GetDelegate<SQLite3.ThreadsafeDelegate>("sqlite3_threadsafe"),
                Open = _sqliteLibrary.GetDelegate<SQLite3.OpenDelegate>("sqlite3_open"),
                OpenV2 = _sqliteLibrary.GetDelegate<SQLite3.OpenV2Delegate>("sqlite3_open_v2"),
                OpenV2Bytes = _sqliteLibrary.GetDelegate<SQLite3.OpenV2BytesDelegate>("sqlite3_open_v2"),
                Open16 = _sqliteLibrary.GetDelegate<SQLite3.Open16Delegate>("sqlite3_open16"),
                EnableLoadExtension = _sqliteLibrary.GetDelegate<SQLite3.EnableLoadExtensionDelegate>("sqlite3_enable_load_extension"),
                Close = _sqliteLibrary.GetDelegate<SQLite3.CloseDelegate>("sqlite3_close"),
                Close2 = _sqliteLibrary.GetDelegate<SQLite3.Close2Delegate>("sqlite3_close_v2"),
                Initialize = _sqliteLibrary.GetDelegate<SQLite3.InitializeDelegate>("sqlite3_initialize"),
                Shutdown = _sqliteLibrary.GetDelegate<SQLite3.ShutdownDelegate>("sqlite3_shutdown"),
                Config = _sqliteLibrary.GetDelegate<SQLite3.ConfigDelegate>("sqlite3_config"),
                SetDirectory = _sqliteLibrary.GetDelegate<SQLite3.SetDirectoryDelegate>("sqlite3_win32_set_directory"),
                BusyTimeout = _sqliteLibrary.GetDelegate<SQLite3.BusyTimeoutDelegate>("sqlite3_busy_timeout"),
                Changes = _sqliteLibrary.GetDelegate<SQLite3.ChangesDelegate>("sqlite3_changes"),
                Prepare2String = _sqliteLibrary.GetDelegate<SQLite3.Prepare2StringDelegate>("sqlite3_prepare_v2"),
#if NETFX_CORE
                Prepare2Bytes = SqliteLibrary.GetDelegate<SQLite3.Prepare2BytesDelegate>("sqlite3_prepare_v2"),
#endif
                Step = _sqliteLibrary.GetDelegate<SQLite3.StepDelegate>("sqlite3_step"),
                Reset = _sqliteLibrary.GetDelegate<SQLite3.ResetDelegate>("sqlite3_reset"),
                Finalize = _sqliteLibrary.GetDelegate<SQLite3.FinalizeDelegate>("sqlite3_finalize"),
                LastInsertRowid = _sqliteLibrary.GetDelegate<SQLite3.LastInsertRowidDelegate>("sqlite3_last_insert_rowid"),
                Errmsg = _sqliteLibrary.GetDelegate<SQLite3.ErrmsgDelegate>("sqlite3_errmsg16"),
                BindParameterIndex = _sqliteLibrary.GetDelegate<SQLite3.BindParameterIndexDelegate>("sqlite3_bind_parameter_index"),
                BindNull = _sqliteLibrary.GetDelegate<SQLite3.BindNullDelegate>("sqlite3_bind_null"),
                BindInt = _sqliteLibrary.GetDelegate<SQLite3.BindIntDelegate>("sqlite3_bind_int"),
                BindInt64 = _sqliteLibrary.GetDelegate<SQLite3.BindInt64Delegate>("sqlite3_bind_int64"),
                BindDouble = _sqliteLibrary.GetDelegate<SQLite3.BindDoubleDelegate>("sqlite3_bind_double"),
                BindText = _sqliteLibrary.GetDelegate<SQLite3.BindTextDelegate>("sqlite3_bind_text16"),
                BindBlob = _sqliteLibrary.GetDelegate<SQLite3.BindBlobDelegate>("sqlite3_bind_blob"),
                ColumnCount = _sqliteLibrary.GetDelegate<SQLite3.ColumnCountDelegate>("sqlite3_column_count"),
                ColumnName = _sqliteLibrary.GetDelegate<SQLite3.ColumnNameDelegate>("sqlite3_column_name"),
                ColumnName16Internal = _sqliteLibrary.GetDelegate<SQLite3.ColumnName16InternalDelegate>("sqlite3_column_name16"),
                ColumnType = _sqliteLibrary.GetDelegate<SQLite3.ColumnTypeDelegate>("sqlite3_column_type"),
                ColumnInt = _sqliteLibrary.GetDelegate<SQLite3.ColumnIntDelegate>("sqlite3_column_int"),
                ColumnInt64 = _sqliteLibrary.GetDelegate<SQLite3.ColumnInt64Delegate>("sqlite3_column_int64"),
                ColumnDouble = _sqliteLibrary.GetDelegate<SQLite3.ColumnDoubleDelegate>("sqlite3_column_double"),
                ColumnText = _sqliteLibrary.GetDelegate<SQLite3.ColumnTextDelegate>("sqlite3_column_text"),
                ColumnText16 = _sqliteLibrary.GetDelegate<SQLite3.ColumnText16Delegate>("sqlite3_column_text16"),
                ColumnBlob = _sqliteLibrary.GetDelegate<SQLite3.ColumnBlobDelegate>("sqlite3_column_blob"),
                ColumnBytes = _sqliteLibrary.GetDelegate<SQLite3.ColumnBytesDelegate>("sqlite3_column_bytes"),
                GetResult = _sqliteLibrary.GetDelegate<SQLite3.GetResultDelegate>("sqlite3_errcode"),
                ExtendedErrCode = _sqliteLibrary.GetDelegate<SQLite3.ExtendedErrCodeDelegate>("sqlite3_extended_errcode"),
                LibVersionNumber = _sqliteLibrary.GetDelegate<SQLite3.LibVersionNumberDelegate>("sqlite3_libversion_number"),
                BackupInit = _sqliteLibrary.GetDelegate<SQLite3.BackupInitDelegate>("sqlite3_backup_init"),
                BackupStep = _sqliteLibrary.GetDelegate<SQLite3.BackupStepDelegate>("sqlite3_backup_step"),
                BackupFinish = _sqliteLibrary.GetDelegate<SQLite3.BackupFinishDelegate>("sqlite3_backup_finish")
            };

            SQLite3.DllCallbacks = callbacks;
        }
    }
}