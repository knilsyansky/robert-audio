namespace YtAudioDownloader.Tests;

public class YtDlpCommandTests
{
    private const string Url = "https://www.youtube.com/watch?v=Ex-dEn5KAsY";

    [Fact]
    public void BuildDownloadArguments_passes_the_bundled_deno()
    {
        List<string> args = YtDlpCommand.BuildDownloadArguments(Url, @"C:\tmp\job", @"C:\app\tools\deno.exe");

        int index = args.IndexOf("--js-runtimes");
        Assert.True(index >= 0);
        Assert.Equal(@"deno:C:\app\tools\deno.exe", args[index + 1]);
    }

    [Fact]
    public void BuildDownloadArguments_without_deno_has_no_js_runtimes()
    {
        Assert.DoesNotContain("--js-runtimes", YtDlpCommand.BuildDownloadArguments(Url, @"C:\tmp\job", null));
    }

    [Fact]
    public void BuildDownloadArguments_ignores_user_config_and_ends_with_the_url()
    {
        List<string> args = YtDlpCommand.BuildDownloadArguments(Url, @"C:\tmp\job", null);

        Assert.Contains("--ignore-config", args);
        Assert.Contains("--no-update", args);
        Assert.Equal(@"C:\tmp\job\download.%(ext)s", args[args.IndexOf("-o") + 1]);
        Assert.Equal(new[] { "--", Url }, args.TakeLast(2));
    }

    [Fact]
    public void BuildDownloadArguments_forces_utf8_output_so_cyrillic_is_readable()
    {
        List<string> args = YtDlpCommand.BuildDownloadArguments(Url, @"C:\tmp\job", null);

        int index = args.IndexOf("--encoding");
        Assert.True(index >= 0, "--encoding missing");
        Assert.Equal("utf-8", args[index + 1]);
    }

    [Fact]
    public void BuildUpdateArguments_switches_to_nightly()
    {
        Assert.Equal(new[] { "--ignore-config", "--update-to", "nightly" }, YtDlpCommand.BuildUpdateArguments());
    }

    [Fact]
    public void ReadLine_collects_title_duration_and_file()
    {
        var info = new YtDlpDownloadInfo();

        Assert.Null(YtDlpCommand.ReadLine("TITLE Crypt of The Seetherdancer", info));
        Assert.Null(YtDlpCommand.ReadLine("DURATION 205", info));
        Assert.Null(YtDlpCommand.ReadLine(@"FILE C:\tmp\job\download.m4a", info));

        Assert.Equal("Crypt of The Seetherdancer", info.Title);
        Assert.Equal(TimeSpan.FromSeconds(205), info.Duration);
        Assert.Equal(@"C:\tmp\job\download.m4a", info.FilePath);
    }

    [Theory]
    [InlineData("DURATION 205.5", 205.5)]
    [InlineData("DURATION NA", null)]
    [InlineData("DURATION NaN", null)]
    public void ReadLine_parses_duration(string line, double? expectedSeconds)
    {
        var info = new YtDlpDownloadInfo();
        YtDlpCommand.ReadLine(line, info);

        Assert.Equal(expectedSeconds is double s ? TimeSpan.FromSeconds(s) : (TimeSpan?)null, info.Duration);
    }

    [Fact]
    public void ReadLine_ignores_non_finite_progress()
    {
        var info = new YtDlpDownloadInfo();
        Assert.Null(YtDlpCommand.ReadLine("PROGRESS NaN 100 NA", info));
    }

    [Fact]
    public void ReadLine_returns_download_progress()
    {
        var info = new YtDlpDownloadInfo();

        Assert.Equal(1024.0 / 3315458, YtDlpCommand.ReadLine("PROGRESS 1024 3315458 NA", info)!.Value, 9);
        Assert.Equal(0.25, YtDlpCommand.ReadLine("PROGRESS 1000 NA 4000.0", info));
        Assert.Null(YtDlpCommand.ReadLine("PROGRESS 1000 NA NA", info));
        Assert.Null(YtDlpCommand.ReadLine("[download] Destination: x", info));
        Assert.Null(info.Title);
    }

    [Theory]
    [InlineData("ERROR: unable to download video data: HTTP Error 403: Forbidden", YtDlpFailure.Blocked)]
    [InlineData("ERROR: [youtube] x: Sign in to confirm you're not a bot. Use --cookies-from-browser", YtDlpFailure.Blocked)]
    [InlineData("ERROR: Unable to download webpage: HTTP Error 429: Too Many Requests", YtDlpFailure.Blocked)]
    [InlineData("ERROR: [youtube] x: Private video. Sign in if you've been granted access to this video", YtDlpFailure.Unavailable)]
    [InlineData("ERROR: [youtube] x: Sign in to confirm your age. This video may be inappropriate for some users.", YtDlpFailure.Unavailable)]
    [InlineData("ERROR: [youtube] x: Video unavailable", YtDlpFailure.Unavailable)]
    [InlineData("ERROR: [generic] 'abc' is not a valid URL.", YtDlpFailure.InvalidUrl)]
    [InlineData("ERROR: Unsupported URL: https://example.com/", YtDlpFailure.InvalidUrl)]
    [InlineData("ERROR: [youtube] x: Unable to download webpage: <urlopen error [Errno 11001] getaddrinfo failed>", YtDlpFailure.Network)]
    [InlineData("ERROR: something nobody has seen before", YtDlpFailure.Unknown)]
    public void Classify_recognises_common_failures(string stderr, YtDlpFailure expected)
    {
        Assert.Equal(expected, YtDlpCommand.Classify(stderr));
    }
}
