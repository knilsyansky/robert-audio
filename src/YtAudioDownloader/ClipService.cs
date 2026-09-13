namespace YtAudioDownloader;

internal sealed record ClipRequest(string Url, TimeSpan Start, TimeSpan End, bool Fade, double FadeSeconds, bool Normalize, string OutputFolder);

internal sealed record ClipResult(string OutputPath, string? Title);

internal enum ClipStage { Updating, Downloading, Converting }

// Fraction is null while the stage has no measurable progress.
internal sealed record ClipProgress(ClipStage Stage, double? Fraction);

// Download → (if YouTube blocked it: update yt-dlp and retry once) → cut, fade, normalize → MP3.
internal sealed class ClipService(IAudioDownloader downloader, IAudioConverter converter)
{
    public async Task<ClipResult> CreateClipAsync(ClipRequest request, IProgress<ClipProgress>? progress, CancellationToken cancellationToken)
    {
        string tempDir = Path.Combine(AppPaths.TempRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string? outputPath = null;
        try
        {
            Directory.CreateDirectory(request.OutputFolder);
            DownloadedAudio audio = await DownloadWithRetryAsync(request.Url, tempDir, progress, cancellationToken).ConfigureAwait(false);

            TimeSpan end = request.End;
            if (audio.Duration is TimeSpan duration)
            {
                if (request.Start >= duration)
                    throw new ClipOutOfRangeException(duration);
                if (end > duration)
                    end = duration;
            }
            TimeSpan clipLength = end - request.Start;

            outputPath = OutputNaming.GetAvailablePath(request.OutputFolder, OutputNaming.BuildFileName(audio.Title, request.Start, end));
            string? filter = FfmpegCommand.BuildFilter(clipLength, request.Fade, request.FadeSeconds, request.Normalize);

            progress?.Report(new ClipProgress(ClipStage.Converting, 0));
            await converter.ConvertAsync(
                FfmpegCommand.BuildArguments(audio.FilePath, outputPath, request.Start, clipLength, filter),
                clipLength,
                StageProgress(progress, ClipStage.Converting),
                cancellationToken).ConfigureAwait(false);

            Log.Write("Saved " + outputPath);
            return new ClipResult(outputPath, audio.Title);
        }
        catch when (outputPath != null)
        {
            TryDelete(() => File.Delete(outputPath));
            throw;
        }
        finally
        {
            TryDelete(() => Directory.Delete(tempDir, recursive: true));
        }
    }

    private async Task<DownloadedAudio> DownloadWithRetryAsync(
        string url, string tempDir, IProgress<ClipProgress>? progress, CancellationToken cancellationToken)
    {
        IProgress<double>? downloadProgress = StageProgress(progress, ClipStage.Downloading);
        progress?.Report(new ClipProgress(ClipStage.Downloading, null));
        try
        {
            return await downloader.DownloadAudioAsync(url, tempDir, downloadProgress, cancellationToken).ConfigureAwait(false);
        }
        catch (YtDlpException ex) when (ex.Failure is YtDlpFailure.Blocked or YtDlpFailure.Unknown)
        {
            Log.Write($"Download failed ({ex.Failure}); updating yt-dlp and retrying once.");
            progress?.Report(new ClipProgress(ClipStage.Updating, null));
            await downloader.UpdateAsync(cancellationToken).ConfigureAwait(false);

            foreach (string file in Directory.GetFiles(tempDir))
                File.Delete(file);
            progress?.Report(new ClipProgress(ClipStage.Downloading, null));
            return await downloader.DownloadAudioAsync(url, tempDir, downloadProgress, cancellationToken).ConfigureAwait(false);
        }
    }

    private static IProgress<double>? StageProgress(IProgress<ClipProgress>? progress, ClipStage stage) =>
        progress is null ? null : new ActionProgress<double>(fraction => progress.Report(new ClipProgress(stage, fraction)));

    private static void TryDelete(Action delete)
    {
        try
        {
            delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed class ActionProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
