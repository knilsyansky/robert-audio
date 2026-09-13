using System.Globalization;
using System.Text.RegularExpressions;

namespace YtAudioDownloader;

internal static partial class YouTubeUrl
{
    // Reads the start time YouTube puts into shared links ("Share → Start at"): t=95, t=95s, t=1m35s, t=1h2m3s, start=95.
    public static TimeSpan? TryGetStartTime(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? uri))
            return null;

        string parameters = uri.Query.TrimStart('?') + "&" + uri.Fragment.TrimStart('#');
        foreach (string pair in parameters.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=');
            if (equals <= 0 || pair[..equals] is not ("t" or "start"))
                continue;
            if (ParseTime(Uri.UnescapeDataString(pair[(equals + 1)..])) is TimeSpan time)
                return time;
        }
        return null;
    }

    private static TimeSpan? ParseTime(string value)
    {
        Match match = TimeRegex().Match(value);
        if (value.Length == 0 || !match.Success)
            return null;

        int Part(int group) => match.Groups[group].Success ? int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture) : 0;
        return new TimeSpan(Part(1), Part(2), Part(3));
    }

    [GeneratedRegex("^(?:([0-9]{1,4})h)?(?:([0-9]{1,6})m)?(?:([0-9]{1,7})s?)?$")]
    private static partial Regex TimeRegex();
}
