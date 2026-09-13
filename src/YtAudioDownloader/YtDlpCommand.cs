using System.Globalization;

namespace YtAudioDownloader;

public enum YtDlpFailure { Blocked, Unavailable, InvalidUrl, Network, Unknown }

// What yt-dlp reported about a download, collected line by line.
internal sealed class YtDlpDownloadInfo
{
    public string? Title { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? FilePath { get; set; }
}

internal static class YtDlpCommand
{
    private const string TitlePrefix = "TITLE ";
    private const string DurationPrefix = "DURATION ";
    private const string FilePrefix = "FILE ";
    private const string ProgressPrefix = "PROGRESS ";

    public static List<string> BuildDownloadArguments(string url, string tempDir, string? denoPath)
    {
        var args = new List<string>
        {
            "--ignore-config", "--encoding", "utf-8", "--no-update", "--no-playlist", "--no-part", "--no-mtime",
            "-f", "bestaudio[ext=m4a]/bestaudio",
        };
        if (denoPath != null)
        {
            args.Add("--js-runtimes");
            args.Add("deno:" + denoPath);
        }
        args.AddRange(new[]
        {
            "--no-simulate", "--progress", "--newline",
            "--progress-template", "download:" + ProgressPrefix + "%(progress.downloaded_bytes)s %(progress.total_bytes)s %(progress.total_bytes_estimate)s",
            "--print", "before_dl:" + TitlePrefix + "%(title)s",
            "--print", "before_dl:" + DurationPrefix + "%(duration)s",
            "--print", "after_move:" + FilePrefix + "%(filepath)s",
            "-o", Path.Combine(tempDir, "download.%(ext)s"),
            "--", url,
        });
        return args;
    }

    public static List<string> BuildUpdateArguments() => new() { "--ignore-config", "--update-to", "nightly" };

    // Records TITLE/DURATION/FILE lines into info; returns the download fraction (0..1) for PROGRESS lines, otherwise null.
    public static double? ReadLine(string line, YtDlpDownloadInfo info)
    {
        if (line.StartsWith(ProgressPrefix, StringComparison.Ordinal))
            return ParseProgress(line[ProgressPrefix.Length..]);

        if (line.StartsWith(TitlePrefix, StringComparison.Ordinal))
            info.Title = line[TitlePrefix.Length..];
        else if (line.StartsWith(DurationPrefix, StringComparison.Ordinal) &&
                 TryParseNumber(line[DurationPrefix.Length..], out double seconds) && seconds > 0)
            info.Duration = TimeSpan.FromSeconds(seconds);
        else if (line.StartsWith(FilePrefix, StringComparison.Ordinal))
            info.FilePath = line[FilePrefix.Length..];
        return null;
    }

    public static YtDlpFailure Classify(string stderr)
    {
        if (ContainsAny(stderr, "HTTP Error 403", "HTTP Error 429", "not a bot", "Requested format is not available",
                "Signature extraction failed", "nsig extraction failed"))
            return YtDlpFailure.Blocked;
        if (ContainsAny(stderr, "is not a valid URL", "Unsupported URL", "Incomplete YouTube ID"))
            return YtDlpFailure.InvalidUrl;
        if (ContainsAny(stderr, "Private video", "Video unavailable", "This video is unavailable", "confirm your age",
                "members-only", "This live event will begin", "Premieres in"))
            return YtDlpFailure.Unavailable;
        if (ContainsAny(stderr, "getaddrinfo failed", "Failed to resolve", "timed out", "Connection refused",
                "Connection reset", "Network is unreachable", "No route to host", "Unable to download webpage"))
            return YtDlpFailure.Network;
        return YtDlpFailure.Unknown;
    }

    // "downloaded total estimate"; yt-dlp prints NA for unknown values.
    private static double? ParseProgress(string values)
    {
        string[] parts = values.Split(' ');
        if (parts.Length != 3 || !TryParseNumber(parts[0], out double downloaded))
            return null;
        if (!TryParseNumber(parts[1], out double total) && !TryParseNumber(parts[2], out total))
            return null;
        return total > 0 ? Math.Clamp(downloaded / total, 0, 1) : null;
    }

    private static bool TryParseNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    private static bool ContainsAny(string text, params string[] needles) =>
        needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
}
