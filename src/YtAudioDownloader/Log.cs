using System.Globalization;
using System.Text;

namespace YtAudioDownloader;

// One file per day in the data folder; "Copy details" points the user here.
internal static class Log
{
    private static readonly object Gate = new();

    public static string CurrentFile =>
        Path.Combine(AppPaths.LogsDir, DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.LogsDir);
                string time = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                File.AppendAllText(CurrentFile, $"[{time}] {message}{Environment.NewLine}", Encoding.UTF8);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging must never break the app.
        }
    }

    public static void DeleteOldFiles(int keep = 14)
    {
        try
        {
            if (!Directory.Exists(AppPaths.LogsDir))
                return;
            foreach (string file in Directory.GetFiles(AppPaths.LogsDir, "*.log").OrderByDescending(f => f, StringComparer.Ordinal).Skip(keep))
                File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
