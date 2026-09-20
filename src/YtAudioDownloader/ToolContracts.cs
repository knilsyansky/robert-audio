namespace YtAudioDownloader;

internal sealed record DownloadedAudio(string FilePath, string? Title, TimeSpan? Duration);

internal sealed record UpdateOutcome(bool Succeeded, string? Version);

internal interface IAudioDownloader
{
    Task<DownloadedAudio> DownloadAudioAsync(string url, string tempDir, IProgress<double>? progress, CancellationToken cancellationToken);

    Task<UpdateOutcome> UpdateAsync(CancellationToken cancellationToken);
}

internal interface IAudioConverter
{
    Task ConvertAsync(IReadOnlyList<string> arguments, TimeSpan clipLength, IProgress<double>? progress, CancellationToken cancellationToken);
}
