using System.ComponentModel;

namespace YtAudioDownloader;

// Runs the app's own self-updating copy of yt-dlp, kept in the data folder so it is always writable.
internal sealed class YtDlpClient(string exePath, string bundledExePath, string? denoPath) : IAudioDownloader
{
    private static readonly TimeSpan UpdateTimeout = TimeSpan.FromSeconds(90);

    public static YtDlpClient CreateDefault()
    {
        string deno = Path.Combine(AppPaths.BundledToolsDir, "deno.exe");
        return new YtDlpClient(
            Path.Combine(AppPaths.DataDir, "yt-dlp.exe"),
            Path.Combine(AppPaths.BundledToolsDir, "yt-dlp.exe"),
            File.Exists(deno) ? deno : null);
    }

    // Returns the working copy's version, restoring it from the bundled copy when it is missing or broken.
    public async Task<string> EnsureReadyAsync(CancellationToken cancellationToken)
    {
        if (await TryGetVersionAsync(cancellationToken).ConfigureAwait(false) is string version)
            return version;

        if (!File.Exists(bundledExePath))
            throw new ToolMissingException("yt-dlp.exe", bundledExePath);

        Directory.CreateDirectory(Path.GetDirectoryName(exePath)!);
        File.Copy(bundledExePath, exePath, overwrite: true);
        Log.Write($"Copied bundled yt-dlp to {exePath}");

        return await TryGetVersionAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new ToolMissingException("yt-dlp.exe", exePath);
    }

    public async Task<UpdateOutcome> UpdateAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(UpdateTimeout);
        bool succeeded;
        try
        {
            ProcessResult result = await ProcessRunner.RunAsync(exePath, YtDlpCommand.BuildUpdateArguments(), null, timeout.Token)
                .ConfigureAwait(false);
            succeeded = result.ExitCode == 0;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Log.Write("yt-dlp update timed out");
            succeeded = false;
        }
        catch (Win32Exception ex)
        {
            Log.Write("yt-dlp update could not start: " + ex.Message);
            succeeded = false;
        }
        return new UpdateOutcome(succeeded, await TryGetVersionAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<DownloadedAudio> DownloadAudioAsync(string url, string tempDir, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (!File.Exists(exePath))
            throw new ToolMissingException("yt-dlp.exe", exePath);

        var info = new YtDlpDownloadInfo();
        ProcessResult result = await ProcessRunner.RunAsync(
            exePath,
            YtDlpCommand.BuildDownloadArguments(url, tempDir, denoPath),
            line =>
            {
                if (YtDlpCommand.ReadLine(line, info) is double fraction)
                    progress?.Report(fraction);
            },
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
            throw new YtDlpException(YtDlpCommand.Classify(result.StdErr), result.StdErr);
        if (info.FilePath is null || !File.Exists(info.FilePath))
            throw new YtDlpException(YtDlpFailure.Unknown, "yt-dlp finished without reporting the downloaded file." + Environment.NewLine + result.StdErr);

        return new DownloadedAudio(info.FilePath, info.Title, info.Duration);
    }

    private async Task<string?> TryGetVersionAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(exePath))
            return null;
        try
        {
            ProcessResult result = await ProcessRunner.RunAsync(exePath, new[] { "--version" }, null, cancellationToken).ConfigureAwait(false);
            string version = result.StdOut.Trim();
            return result.ExitCode == 0 && version.Length > 0 ? version : null;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }
}
