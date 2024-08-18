using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AsyncLoggers.Sqlite;

internal static class SQLiteLoader
{
    private const string SQLiteBaseUrl = "https://www.sqlite.org/";
    private const string SQLiteDownloadPage = "https://www.sqlite.org/download.html";
    private const string SubFolder = "sqlite_native";

    public static async Task<bool> EnsureSQLite()
    {
        AsyncLoggers.Log.LogDebug("Ensuring SQLite native library is available...");

        // Determine the folder path for the native DLLs relative to the current assembly
        var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var sqliteFolderPath = Path.Combine(assemblyLocation!, SubFolder);
        Directory.CreateDirectory(sqliteFolderPath);

        // Determine the correct DLL file name based on architecture
        const string dllFileName = "sqlite3.dll";
        var dllPath = Path.Combine(sqliteFolderPath, dllFileName);

        // Check if the DLL is already present
        if (!File.Exists(dllPath))
            // Fetch and download the DLL if it's missing
            if (!await FetchSQLiteDll(sqliteFolderPath))
            {
                AsyncLoggers.Log.LogError("Failed to fetch and download the SQLite DLL.");
                return false;
            }

        // Load the DLL dynamically
        return LoadNativeDll(dllPath);
    }

    private static async Task<bool> FetchSQLiteDll(string destinationFolder)
    {
        try
        {
            // Fetch the HTML content of the SQLite download page
            using var client = new HttpClient();
            
            AsyncLoggers.Log.LogDebug($"Fetching SQLite download page from {SQLiteDownloadPage}...");
            var htmlContent = await client.GetStringAsync(SQLiteDownloadPage);

            // Extract the CSV comment block
            var csvPattern = @"<!--\s*Download product data for scripts to read\s*(?<csv>.*?)\s*-->";
            var match = Regex.Match(htmlContent, csvPattern, RegexOptions.Singleline);
            if (!match.Success)
            {
                AsyncLoggers.Log.LogError("Failed to find the download data comment in the HTML.");
                return false;
            }

            var csvContent = match.Groups["csv"].Value;

            // Parse the CSV to find the correct URL
            var lines = csvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var arch = Environment.Is64BitOperatingSystem ? "win64-x64" : "win32-x86";
            var dllLine = lines.FirstOrDefault(line => line.Contains(arch));

            if (dllLine == null)
            {
                AsyncLoggers.Log.LogError($"Could not find a matching download link for architecture: {arch}");
                return false;
            }

            // Extract the relative URL from the CSV line
            var columns = dllLine.Split(',');
            var relativeUrl = columns[2];
            var downloadUrl = SQLiteBaseUrl + relativeUrl;

            // Download the ZIP file containing the DLL
            var zipFileName = Path.GetTempFileName();
            AsyncLoggers.Log.LogWarning($"Downloading SQLite native library from {downloadUrl}...");
            var data = await client.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(zipFileName, data);

            // Extract the DLL from the ZIP file
            AsyncLoggers.Log.LogDebug("Extracting SQLite DLL...");
            ZipFile.ExtractToDirectory(zipFileName, destinationFolder, true);

            // Clean up the temporary ZIP file
            File.Delete(zipFileName);
            AsyncLoggers.Log.LogInfo("SQLite DLL download and extraction completed successfully.");

            return true;
        }
        catch (Exception ex)
        {
            AsyncLoggers.Log.LogError($"Error during SQLite DLL fetch: {ex}");
            return false;
        }
    }

    private static bool LoadNativeDll(string dllPath)
    {
        var pDll = LoadLibrary(dllPath);

        if (pDll == IntPtr.Zero)
        {
            AsyncLoggers.Log.LogError(
                $"Failed to load SQLite native DLL from path: {dllPath}. Error code: {Marshal.GetLastWin32Error()}");
            return false;
        }
        
        AsyncLoggers.Log.LogInfo("SQLite DLL loaded successfully.");
        return true;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadLibrary(string dllToLoad);
}