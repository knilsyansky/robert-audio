namespace YtAudioDownloader.Tests;

public class ErrorTextTests
{
    private static readonly UiText Text = Strings.English;

    [Theory]
    [InlineData(YtDlpFailure.Blocked)]
    [InlineData(YtDlpFailure.Unavailable)]
    [InlineData(YtDlpFailure.InvalidUrl)]
    [InlineData(YtDlpFailure.Network)]
    [InlineData(YtDlpFailure.Unknown)]
    public void Yt_dlp_failures_get_their_own_message_and_keep_the_raw_output(YtDlpFailure failure)
    {
        (string message, string details) = ErrorText.Describe(new YtDlpException(failure, "raw stderr"), Text);

        string expected = failure switch
        {
            YtDlpFailure.Blocked => Text.ErrorBlocked,
            YtDlpFailure.Unavailable => Text.ErrorUnavailable,
            YtDlpFailure.InvalidUrl => Text.ErrorInvalidUrl,
            YtDlpFailure.Network => Text.ErrorNetwork,
            _ => Text.ErrorUnknown,
        };
        Assert.Equal(expected, message);
        Assert.Equal("raw stderr", details);
    }

    [Fact]
    public void Missing_tool_names_the_file()
    {
        (string message, _) = ErrorText.Describe(new ToolMissingException("ffmpeg.exe", @"C:\app\tools\ffmpeg.exe"), Text);

        Assert.Contains("ffmpeg.exe", message);
    }

    [Fact]
    public void Out_of_range_shows_the_video_length()
    {
        (string message, _) = ErrorText.Describe(new ClipOutOfRangeException(TimeSpan.FromSeconds(205)), Text);

        Assert.Contains("3:25", message);
    }

    [Fact]
    public void Ffmpeg_failure_keeps_its_output()
    {
        (string message, string details) = ErrorText.Describe(new FfmpegException("ffmpeg said no"), Text);

        Assert.Equal(Text.ErrorConvert, message);
        Assert.Equal("ffmpeg said no", details);
    }

    [Fact]
    public void Anything_else_is_unknown_with_the_full_exception()
    {
        (string message, string details) = ErrorText.Describe(new InvalidOperationException("boom"), Text);

        Assert.Equal(Text.ErrorUnknown, message);
        Assert.Contains("boom", details);
    }
}
