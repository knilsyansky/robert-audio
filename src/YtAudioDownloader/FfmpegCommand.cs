using System.Globalization;

namespace YtAudioDownloader;

internal static class FfmpegCommand
{
    private const string NormalizeFilter = "loudnorm=I=-14:TP=-1.5:LRA=11";
    private const string ProgressPrefix = "out_time_us=";

    // Each fade takes at most a third of the clip, so the middle always plays at full volume.
    public static double EffectiveFadeSeconds(TimeSpan clipLength, double requestedSeconds) =>
        Math.Max(0, Math.Min(requestedSeconds, clipLength.TotalSeconds / 3));

    public static string? BuildFilter(TimeSpan clipLength, bool fade, double fadeSeconds, bool normalize)
    {
        var filters = new List<string>();
        if (normalize)
            filters.Add(NormalizeFilter);

        double fadeLength = fade ? EffectiveFadeSeconds(clipLength, fadeSeconds) : 0;
        if (fadeLength > 0)
        {
            filters.Add($"afade=t=in:st=0:d={Number(fadeLength)}");
            filters.Add($"afade=t=out:st={Number(clipLength.TotalSeconds - fadeLength)}:d={Number(fadeLength)}");
        }
        return filters.Count == 0 ? null : string.Join(",", filters);
    }

    public static List<string> BuildArguments(string inputFile, string outputFile, TimeSpan start, TimeSpan clipLength, string? filter)
    {
        var args = new List<string>
        {
            "-hide_banner", "-nostdin", "-y",
            "-ss", Number(start.TotalSeconds), "-i", inputFile, "-t", Number(clipLength.TotalSeconds), "-vn",
        };
        if (filter != null)
        {
            args.Add("-af");
            args.Add(filter);
        }
        // loudnorm resamples to 192 kHz, so the output rate is set explicitly.
        args.AddRange(new[] { "-ar", "44100", "-c:a", "libmp3lame", "-q:a", "2", "-progress", "pipe:1", "-nostats", outputFile });
        return args;
    }

    // "-progress pipe:1" prints lines like "out_time_us=12345678".
    public static double? TryParseProgress(string line, TimeSpan clipLength)
    {
        if (clipLength <= TimeSpan.Zero || !line.StartsWith(ProgressPrefix, StringComparison.Ordinal))
            return null;
        if (!long.TryParse(line.AsSpan(ProgressPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out long microseconds))
            return null;
        return Math.Clamp(microseconds / 1_000_000.0 / clipLength.TotalSeconds, 0, 1);
    }

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
