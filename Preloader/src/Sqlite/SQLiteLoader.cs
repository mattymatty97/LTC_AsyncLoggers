using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using AsyncLoggers.Proxy.WinAPI;
using BepInEx;
using SQLite;

namespace AsyncLoggers.Sqlite;

public static class SQLiteLoader
{
    const string EolUrl = "https://endoflife.date/api/v1/products/sqlite/releases/latest";
    const string DownloadUrl = "https://www.sqlite.org/{0}/sqlite-dll-win-x64-{1}.zip";
    const string DownloadFileName = "sqlite-dll-win-x64-{0}.zip";
    const string DLLFileName = "sqlite3.dll";
    const int MinLibraryVersion = 350004;

    private static Kernel32.NativeLibrary _sqliteLibrary;

    internal static Kernel32.NativeLibrary EnsureSqliteLibrary()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

        //try to load the library
        if (LoadSqliteLibrary())
            return _sqliteLibrary;

        //download library
        DownloadSqliteLibrary();

        //try to load the library again
        LoadSqliteLibrary();

        return _sqliteLibrary;
    }


    private static bool LoadSqliteLibrary()
    {
        var cacheFolder = Path.Combine(Paths.BepInExRootPath, "cache", AsyncLoggers.NAME);
        var dllPath = Path.Combine(cacheFolder, DLLFileName);
        // Check if the dll exists
        if (File.Exists(dllPath))
        {
            //try to load it
            try
            {
                _sqliteLibrary = new Kernel32.NativeLibrary(dllPath);

                var versionNumberDelegate =
                    _sqliteLibrary.GetSafeDelegate<SQLite3.LibVersionNumberDelegate>("sqlite3_libversion_number");
                
                var versionStringDelegate = 
                    _sqliteLibrary.GetSafeDelegate<SQLite3.LibVersionDelegate>("sqlite3_libversion");
                //try to grab the library version
                if (versionNumberDelegate != null)
                {
                    try
                    {
                        var version = versionNumberDelegate();
                        
                        AsyncLoggers.Log.LogInfo($"Found SQLite library {(versionStringDelegate != null ? Marshal.PtrToStringAnsi(versionStringDelegate()) : version)}");
                        if (version >= MinLibraryVersion)
                            return true;
                        AsyncLoggers.Log.LogWarning($"SQLite library is outdated! {version}");
                    }
                    catch (Exception ex)
                    {
                        AsyncLoggers.Log.LogError("Exception while reading sqlite version: \n" + ex);
                    }
                }
                else
                    AsyncLoggers.Log.LogError("SQLite library does not have a version! ( Wrong DLL? )");
            }
            catch (Win32Exception)
            {
                AsyncLoggers.Log.LogError("could not load SQLite library!");
            }
        }
        else
            AsyncLoggers.Log.LogWarning("missing SQLite library!");

        return false;
    }

    private static void DownloadSqliteLibrary()
    {
        var cacheFolder = Path.Combine(Paths.BepInExRootPath, "cache", AsyncLoggers.NAME);
        var eolRegex = new Regex("\"latest\":{\"name\":\"(\\d+\\.\\d+\\.\\d+)\",\"date\":\"(\\d+-\\d+-\\d+)\"");

        AsyncLoggers.Log.LogWarning("Downloading SQLite library...");
        try
        {
            AsyncLoggers.Log.LogInfo("Fetching latest SQLite version from https://endoflife.date");
            var eolJson = WinHttp.DownloadAsText(EolUrl);

            var matches = eolRegex.Matches(eolJson);

            if (matches.Count == 0)
                return;

            var match = matches[0];

            var version = match.Groups[1].Value.Split(".");
            var date = match.Groups[2].Value.Split("-");
            var sb = new StringBuilder(version[0]);
            for (var i = 1; i < 4; i++)
            {
                var val = i < version.Length ? int.Parse(version[i]) : 0;
                sb.Append(val.ToString("D2"));
            }

            var versionString = sb.ToString();
            AsyncLoggers.Log.LogDebug($"Lateral SQLite version: {versionString}");

            var downloadUrl = string.Format(DownloadUrl, date[0], versionString);
            var downloadFileName = string.Format(DownloadFileName, versionString);

            Directory.CreateDirectory(cacheFolder);
            
            AsyncLoggers.Log.LogMessage($"Downloading: {downloadUrl} as {downloadFileName}");
            WinHttp.DownloadFile(downloadUrl, downloadFileName);

            using var zipStream = File.OpenRead(downloadFileName);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            archive.ExtractToDirectory(cacheFolder);
        }
        catch (Exception ex)
        {
            AsyncLoggers.Log.LogError("Exception while downloading sqlite library: \n" + ex);
        }
    }

    internal static T GetDelegate<T>(this Kernel32.NativeLibrary library, string functionName) where T : class
    {
        if (!library.TryGetExportedFunctionOffset(functionName, out var functionOffset)) 
            return null;
        
        var function = new Kernel32.NativeFunction<T>(library, functionName, functionOffset);
        return function.Delegate;
    }
    
    internal static T GetSafeDelegate<T>(this Kernel32.NativeLibrary library, string functionName) where T : class
    {
        if (!library.TryGetExportedFunctionOffset(functionName, out var functionOffset)) 
            return null;
        
        var function = new Kernel32.NativeFunction<T>(library, functionName, functionOffset);
        return function.SafeDelegate;
    }
}