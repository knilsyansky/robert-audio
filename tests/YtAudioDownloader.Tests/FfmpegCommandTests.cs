using System.Globalization;

namespace YtAudioDownloader.Tests;

public class FfmpegCommandTests
{
    private const string Loudnorm = "loudnorm=I=-14:TP=-1.5:LRA=11";

    [Fact]
    public void BuildFilter_normalizes_then_fades()
    {
        Assert.Equal(
            Loudnorm + ",afade=t=in:st=0:d=3,afade=t=out:st=27:d=3",
            FfmpegCommand.BuildFilter(TimeSpan.FromSeconds(30), fade: true, fadeSeconds: 3, normalize: true));
    }

    [Fact]
    public void BuildFilter_uses_a_dot_on_russian_windows()
    {
        string? filter = WithCulture("ru-RU", () =>
            FfmpegCommand.BuildFilter(TimeSpan.FromSeconds(30.5), fade: true, fadeSeconds: 3, normalize: false));

        Assert.Equal("afade=t=in:st=0:d=3,afade=t=out:st=27.5:d=3", filter);
    }

    [Fact]
    public void BuildFilter_shortens_fades_for_short_clips()
    {
        Assert.Equal(
            "afade=t=in:st=0:d=2,afade=t=out:st=4:d=2",
            FfmpegCommand.BuildFilter(TimeSpan.FromSeconds(6), fade: true, fadeSeconds: 3, normalize: false));
    }

    [Theory]
    [InlineData(false, 3.0, false, null)]
    [InlineData(true, 0.0, false, null)]
    [InlineData(false, 3.0, true, Loudnorm)]
    public void BuildFilter_omits_what_is_turned_off(bool fade, double fadeSeconds, bool normalize, string? expected)
    {
        Assert.Equal(expected, FfmpegCommand.BuildFilter(TimeSpan.FromSeconds(30), fade, fadeSeconds, normalize));
    }

    [Fact]
    public void EffectiveFadeSeconds_is_at_most_a_third_of_the_clip()
    {
        Assert.Equal(3, FfmpegCommand.EffectiveFadeSeconds(TimeSpan.FromSeconds(30), 3));
        Assert.Equal(1, FfmpegCommand.EffectiveFadeSeconds(TimeSpan.FromSeconds(3), 3));
    }

    [Fact]
    public void BuildArguments_cuts_converts_and_reports_progress()
    {
        List<string> args = WithCulture("ru-RU", () =>
            FfmpegCommand.BuildArguments("in.m4a", "out.mp3", TimeSpan.FromSeconds(95.5), TimeSpan.FromSeconds(30), "afade=t=in:st=0:d=3"));

        Assert.Equal(new[]
        {
            "-hide_banner", "-nostdin", "-y", "-ss", "95.5", "-i", "in.m4a", "-t", "30", "-vn",
            "-af", "afade=t=in:st=0:d=3",
            "-ar", "44100", "-c:a", "libmp3lame", "-q:a", "2", "-progress", "pipe:1", "-nostats", "out.mp3",
        }, args);
    }

    [Fact]
    public void BuildArguments_without_filter_has_no_af()
    {
        List<string> args = FfmpegCommand.BuildArguments("in.m4a", "out.mp3", TimeSpan.Zero, TimeSpan.FromSeconds(30), null);

        Assert.DoesNotContain("-af", args);
    }

    [Theory]
    [InlineData("out_time_us=15000000", 0.5)]
    [InlineData("out_time_us=45000000", 1.0)]
    [InlineData("out_time_us=0", 0.0)]
    public void TryParseProgress_reads_out_time(string line, double expected)
    {
        Assert.Equal(expected, FfmpegCommand.TryParseProgress(line, TimeSpan.FromSeconds(30)));
    }

    [Theory]
    [InlineData("out_time_us=N/A")]
    [InlineData("progress=end")]
    [InlineData("bitrate=128.0kbits/s")]
    public void TryParseProgress_ignores_other_lines(string line)
    {
        Assert.Null(FfmpegCommand.TryParseProgress(line, TimeSpan.FromSeconds(30)));
    }

    private static T WithCulture<T>(string name, Func<T> action)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(name);
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
