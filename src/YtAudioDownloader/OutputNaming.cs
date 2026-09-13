using System.Text;
using System.Text.RegularExpressions;

namespace YtAudioDownloader;

internal static partial class OutputNaming
{
    private const int MaxTitleLength = 120;
    private const string FallbackTitle = "audio";

    // Turns a video title into something Windows accepts as (part of) a file name.
    public static string SanitizeTitle(string? title)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder();
        foreach (char c in title ?? string.Empty)
            builder.Append(char.IsControl(c) || Array.IndexOf(invalid, c) >= 0 ? ' ' : c);

        string cleaned = Whitespace().Replace(builder.ToString(), " ").Trim();
        if (cleaned.Length > MaxTitleLength)
        {
            int cut = char.IsHighSurrogate(cleaned[MaxTitleLength - 1]) ? MaxTitleLength - 1 : MaxTitleLength;
            cleaned = cleaned[..cut].TrimEnd();
        }
        return cleaned.Length == 0 ? FallbackTitle : cleaned;
    }

    public static string BuildFileName(string? title, TimeSpan start, TimeSpan end) =>
        $"{SanitizeTitle(title)} ({TimeInput.FormatCompact(start)}-{TimeInput.FormatCompact(end)}).mp3";

    // Adds " (2)", " (3)", ... so an existing file is never overwritten.
    public static string GetAvailablePath(string folder, string fileName, Func<string, bool>? exists = null)
    {
        exists ??= File.Exists;
        string stem = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        string candidate = Path.Combine(folder, fileName);
        for (int n = 2; exists(candidate); n++)
            candidate = Path.Combine(folder, $"{stem} ({n}){extension}");
        return candidate;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
