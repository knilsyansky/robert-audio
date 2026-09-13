using System.Diagnostics;

namespace YtAudioDownloader.Tests;

public class ProcessRunnerTests
{
    [Fact]
    public async Task Returns_exit_code_and_stderr()
    {
        ProcessResult result = await ProcessRunner.RunAsync(
            "cmd.exe", new[] { "/c", "echo oops 1>&2 & exit 3" }, null, CancellationToken.None);

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("oops", result.StdErr);
    }

    [Fact]
    public async Task Streams_utf8_stdout_lines()
    {
        var lines = new List<string>();

        ProcessResult result = await ProcessRunner.RunAsync(
            "powershell.exe",
            new[] { "-NoProfile", "-Command", "[Console]::OutputEncoding = [Text.Encoding]::UTF8; Write-Output 'привет'; Write-Output 'мир'" },
            lines.Add,
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(new[] { "привет", "мир" }, lines);
    }

    [Fact]
    public async Task Cancellation_kills_the_process()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ProcessRunner.RunAsync(
            "ping.exe", new[] { "-n", "30", "127.0.0.1" }, null, cancellation.Token));

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"took {stopwatch.Elapsed}");
    }
}
