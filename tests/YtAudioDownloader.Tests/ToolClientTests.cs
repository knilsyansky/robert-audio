namespace YtAudioDownloader.Tests;

public class ToolClientTests
{
    private static readonly string Missing = Path.Combine(Path.GetTempPath(), "YtAudioDownloader.Tests", "missing");

    [Fact]
    public async Task EnsureReady_without_any_yt_dlp_reports_the_missing_tool()
    {
        var client = new YtDlpClient(Path.Combine(Missing, "local", "yt-dlp.exe"), Path.Combine(Missing, "tools", "yt-dlp.exe"), null);

        ToolMissingException error = await Assert.ThrowsAsync<ToolMissingException>(() => client.EnsureReadyAsync(CancellationToken.None));
        Assert.Equal("yt-dlp.exe", error.ToolName);
    }

    [Fact]
    public async Task Download_without_yt_dlp_reports_the_missing_tool()
    {
        var client = new YtDlpClient(Path.Combine(Missing, "local", "yt-dlp.exe"), Path.Combine(Missing, "tools", "yt-dlp.exe"), null);

        await Assert.ThrowsAsync<ToolMissingException>(() =>
            client.DownloadAudioAsync("https://youtu.be/x", Missing, null, CancellationToken.None));
    }

    [Fact]
    public async Task Convert_without_ffmpeg_reports_the_missing_tool()
    {
        var client = new FfmpegClient(Path.Combine(Missing, "ffmpeg.exe"));

        ToolMissingException error = await Assert.ThrowsAsync<ToolMissingException>(() =>
            client.ConvertAsync(new[] { "-version" }, TimeSpan.FromSeconds(1), null, CancellationToken.None));
        Assert.Equal("ffmpeg.exe", error.ToolName);
    }
}
