using System;
using System.IO;

namespace TopGil;

internal static class DebuggerLog
{
    /*
     *  TODO: write this log to the plugin's folder instead of using Windows temp folder.
     */

    private static string logFilePath;
    private static Configuration config;

    public static string GetLogFilePath()
    {
        return logFilePath;
    }

    public static void Init(Configuration configuration, string? path = null)
    {
        config = configuration;
        logFilePath = path ?? Path.Combine(Path.GetTempPath(), "topgil.debugger.log");

        if (File.Exists(logFilePath))
        {
            var fileInfo = new FileInfo(logFilePath);
            // Delete the log file if it is older than 1 day
            // TODO: this doesn't seem to work - investigate - does the timestamp get updated when we write to the file?
            if (fileInfo.LastWriteTime < DateTime.Now.AddDays(-1))
            {
                File.Delete(logFilePath);
            }
        }
    }

    public static void Write(string message)
    {
        if (config.DebugEnabled)
        {
            if (string.IsNullOrEmpty(logFilePath))
            {
                throw new InvalidOperationException("Log file path is not initialized.");
            }

            string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
            File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
        }
    }
}
