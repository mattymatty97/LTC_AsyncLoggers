using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using BepInEx;
using BepInEx.Logging;

namespace AsyncLoggers.BepInExListeners
{
    public class TsvListener : ILogListener
    {
        public TsvListener(string localPath)
        {
            var num = 1;
            FileStream fileStream;
            for (;
                 !Utility.TryOpenFileStream(Path.Combine(Paths.BepInExRootPath, localPath), FileMode.Create,
                     out fileStream, FileAccess.Write);
                 localPath = string.Format("LogOutput.log.{0}", num++))
            {
                if (num == 5)
                {
                    Logger.LogError("Couldn't open a log file for writing. Skipping log file creation");
                    return;
                }

                Logger.LogWarning("Couldn't open log file '" + localPath +
                                  "' for writing, trying another...");
            }

            LogWriter = TextWriter.Synchronized(new StreamWriter(fileStream, Utility.UTF8NoBom));
            FlushTimer = new Timer((TimerCallback)(o => LogWriter?.Flush()), null, 2000, 2000);
            LogWriter.WriteLine("UUID\tTIMESTAMP\tSOURCE\tLEVEL\tMESSAGE\tSTACKTRACE");
        }

        public TextWriter LogWriter { get; internal set; }

        public Timer FlushTimer { get; internal set; }

        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            StackTrace stacktrace = null;
            if (PluginConfig.StackTraces.Enabled.Value)
            {
                stacktrace = LogContext.Stacktrace;
                if (stacktrace == null)
                    stacktrace = new StackTrace(false);
            }
            
            StringBuilder sb = new StringBuilder();
            sb.Append((int)LogContext.Uuid!).Append("\t");
            sb.Append(LogContext.Timestamp!.Value.ToString("MM/dd/yyyy HH:mm:ss.fffffff")).Append("\t");
            sb.Append(eventArgs.Source.SourceName).Append("\t");
            sb.Append(eventArgs.Level.ToString()).Append("\t");
            sb.Append(WebUtility.HtmlEncode(eventArgs.Data.ToString())).Append("\t");
            sb.Append(WebUtility.HtmlEncode(stacktrace?.ToString())).Append("\t");
            LogWriter.WriteLine(sb.ToString());
        }

        public void Dispose()
        {
            FlushTimer?.Dispose();
            LogWriter?.Flush();
            LogWriter?.Dispose();
        }
    }
}