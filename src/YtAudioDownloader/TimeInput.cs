using System.Globalization;

namespace YtAudioDownloader;

// Times typed by a person: "95", "1:35", "1:02:03", optionally with a fraction ("1:35.5" or "1:35,5").
internal static class TimeInput
{
    private const double MaxSeconds = 100 * 3600;

    public static bool TryParse(string? text, out TimeSpan value)
    {
        value = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string[] parts = text.Trim().Replace(',', '.').Split(':');
        if (parts.Length > 3)
            return false;

        if (!double.TryParse(parts[^1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double seconds))
            return false;
        if (!double.IsFinite(seconds))
            return false;
        if (parts.Length > 1 && seconds >= 60)
            return false;

        double minutes = 0;
        if (parts.Length >= 2)
        {
            if (!TryParseWhole(parts[^2], out long wholeMinutes))
                return false;
            if (parts.Length == 3 && wholeMinutes >= 60)
                return false;
            minutes = wholeMinutes;
        }

        double hours = 0;
        if (parts.Length == 3)
        {
            if (!TryParseWhole(parts[0], out long wholeHours))
                return false;
            hours = wholeHours;
        }

        double total = hours * 3600 + minutes * 60 + seconds;
        if (total >= MaxSeconds)
            return false;

        value = TimeSpan.FromMilliseconds(Math.Round(total * 1000));
        return true;
    }

    // 95.5 s -> "1m35.5s", 3723 s -> "1h02m03s"; used in output file names.
    public static string FormatCompact(TimeSpan time)
    {
        int hours = (int)time.TotalHours;
        return hours > 0
            ? $"{hours}h{time.Minutes:00}m{SecondsPart(time)}s"
            : $"{time.Minutes}m{SecondsPart(time)}s";
    }

    // 95.5 s -> "1:35.5", 3723 s -> "1:02:03"; the format TryParse reads back.
    public static string FormatDisplay(TimeSpan time)
    {
        int hours = (int)time.TotalHours;
        return hours > 0
            ? $"{hours}:{time.Minutes:00}:{SecondsPart(time)}"
            : $"{time.Minutes}:{SecondsPart(time)}";
    }

    private static string SecondsPart(TimeSpan time)
    {
        string seconds = time.Seconds.ToString("00", CultureInfo.InvariantCulture);
        return time.Milliseconds == 0
            ? seconds
            : seconds + "." + time.Milliseconds.ToString("000", CultureInfo.InvariantCulture).TrimEnd('0');
    }

    private static bool TryParseWhole(string text, out long value) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
