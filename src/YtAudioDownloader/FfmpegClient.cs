namespace YtAudioDownloader;

internal sealed class FfmpegClient(string exePath) : IAudioConverter
{
    public static FfmpegClient CreateDefault() => new(Path.Combine(AppPaths.BundledToolsDir, "ffmpeg.exe"));

    public async Task ConvertAsync(IReadOnlyList<string> arguments, TimeSpan clipLength, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (!File.Exists(exePath))
            throw new ToolMissingException("ffmpeg.exe", exePath);

        ProcessResult result = await ProcessRunner.RunAsync(
            exePath,
            arguments,
            line =>
            {
                if (FfmpegCommand.TryParseProgress(line, clipLength) is double fraction)
                    progress?.Report(fraction);
            },
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
            throw new FfmpegException(result.StdErr);
    }
}
