using System.IO;
using System.Text;

namespace JFlightShaker.Service;

public static class AppLog
{
    private const long MaxLogBytes = 2 * 1024 * 1024;
    private static readonly object Sync = new();
    private static string? _logPath;

    public static string LogPath =>
        _logPath ?? Path.Combine(AppContext.BaseDirectory, "Config", "Logs", "JFlightShaker.log");

    public static void Initialize()
    {
        lock (Sync)
        {
            _logPath = Path.Combine(
                AppContext.BaseDirectory, "Config", "Logs", "JFlightShaker.log");
            TryRotate();
            Write("INFO", $"Starting JFlightShaker {BuildInfo.DisplayVersion}");
        }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warning(string message) => Write("WARN", message);

    public static void Error(string message, Exception? exception = null)
        => Write("ERROR", exception == null
            ? message
            : $"{message}{Environment.NewLine}{exception}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Sync)
            {
                string path = LogPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(
                    path,
                    $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] [{level}] {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never make the application fail.
        }
    }

    private static void TryRotate()
    {
        try
        {
            string path = LogPath;
            if (!File.Exists(path) || new FileInfo(path).Length <= MaxLogBytes)
                return;
            File.Move(
                path,
                Path.Combine(Path.GetDirectoryName(path)!, "JFlightShaker.previous.log"),
                true);
        }
        catch
        {
            // A locked or read-only log is non-fatal.
        }
    }
}
