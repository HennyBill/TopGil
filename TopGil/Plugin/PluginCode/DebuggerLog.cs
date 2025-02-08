using System;
using System.IO;

namespace TopGil;

internal static class DebuggerLog
{
    private static string? LogFilePath;
    private static Configuration? Config;
    private static bool IsInitialized = false;

    public static string? GetFullLogFileName()
    {
        return LogFilePath;
    }

    public static void Init(Configuration configuration, int maxLines = 500, int truncateLines = 150)
    {
        Config = configuration;

        string pluginFolder = Plugin.PluginInterface.ConfigDirectory.FullName;

        // Ensure the database path exists
        if (!Directory.Exists(pluginFolder))
        {
            Directory.CreateDirectory(pluginFolder);
        }

        LogFilePath = Path.Combine(pluginFolder, "plugin.log");

        // Truncate the log file if it is too big
        // It's not quite working as expected, so it's commented out for now
        /*if (File.Exists(LogFilePath))
        {
            long elapsedMilliseconds = TimeTools.MeasureExecutionTime(() =>
            {
                var lines = File.ReadAllLines(LogFilePath);
                if (lines.Length > maxLines)
                {
                    File.WriteAllLines(LogFilePath, lines.Skip(truncateLines).ToArray());
                }

                lines = null;
            });
            DebuggerLog.Write($"Log file truncated, took {elapsedMilliseconds}ms");
        }*/

        IsInitialized = true;
    }

    public static void Write(string message)
    {
        if (Config != null && Config.DebugEnabled)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("Log file path is not initialized.");
            }

            string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
            File.AppendAllText(LogFilePath!, logMessage + Environment.NewLine);
        }
    }

	public static bool IsDebugEnabled
	{
		get
		{
			return Config?.DebugEnabled ?? false;
		}
	}
}
