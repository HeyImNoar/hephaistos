using System;
using System.IO;

namespace Hephaistos.Services;

public static class LogService
{
    private static readonly object LockObject = new();

    private static readonly string LogDirectory =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            ),
            "Hephaistos",
            "Logs"
        );

    public static readonly string LogFilePath =
        Path.Combine(
            LogDirectory,
            "hephaistos.log"
        );

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Error(string operation, Exception exception)
    {
        Write(
            "ERROR",
            $"{operation} | {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}"
        );
    }

    private static void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);

            var line =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}" +
                Environment.NewLine;

            lock (LockObject)
            {
                File.AppendAllText(LogFilePath, line);
            }
        }
        catch
        {
            // Le journal ne doit jamais faire planter Hephaistos.
        }
    }
}
