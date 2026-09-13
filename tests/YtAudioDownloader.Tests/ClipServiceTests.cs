namespace YtAudioDownloader.Tests;

public sealed class ClipServiceTests : IDisposable
{
    private readonly string _outputFolder = Path.Combine(Path.GetTempPath(), "YtAudioDownloader.Tests", Guid.NewGuid().ToString("N"));
    private readonly FakeDownloader _downloader = new();
    private readonly FakeConverter _converter = new();

    public void Dispose()
    {
        try
        {
            Directory.Delete(_outputFolder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task Saves_the_clip_named_after_the_video()
    {
        _downloader.Results.Enqueue(Audio());

        ClipResult result = await CreateClip(start: 95, end: 125);

        Assert.Equal(Path.Combine(_outputFolder, "Song (1m35s-2m05s).mp3"), result.OutputPath);
        Assert.True(File.Exists(result.OutputPath));
        AssertOption("-ss", "95");
        AssertOption("-t", "30");
    }

    [Fact]
    public async Task Updates_yt_dlp_and_retries_once_when_blocked()
    {
        _downloader.Results.Enqueue(Fail(YtDlpFailure.Blocked));
        _downloader.Results.Enqueue(Audio());

        ClipResult result = await CreateClip(start: 95, end: 125);

        Assert.True(File.Exists(result.OutputPath));
        Assert.Equal(2, _downloader.DownloadCalls);
        Assert.Equal(1, _downloader.UpdateCalls);
    }

    [Fact]
    public async Task Gives_up_after_the_single_retry()
    {
        _downloader.Results.Enqueue(Fail(YtDlpFailure.Blocked));
        _downloader.Results.Enqueue(Fail(YtDlpFailure.Blocked));

        YtDlpException error = await Assert.ThrowsAsync<YtDlpException>(() => CreateClip(start: 95, end: 125));

        Assert.Equal(YtDlpFailure.Blocked, error.Failure);
        Assert.Equal(2, _downloader.DownloadCalls);
        Assert.Equal(1, _downloader.UpdateCalls);
    }

    [Fact]
    public async Task Does_not_retry_an_unavailable_video()
    {
        _downloader.Results.Enqueue(Fail(YtDlpFailure.Unavailable));

        await Assert.ThrowsAsync<YtDlpException>(() => CreateClip(start: 95, end: 125));

        Assert.Equal(1, _downloader.DownloadCalls);
        Assert.Equal(0, _downloader.UpdateCalls);
    }

    [Fact]
    public async Task Clamps_the_end_to_the_video_length()
    {
        _downloader.Results.Enqueue(Audio(durationSeconds: 205));

        ClipResult result = await CreateClip(start: 190, end: 220);

        Assert.EndsWith("Song (3m10s-3m25s).mp3", result.OutputPath);
        AssertOption("-t", "15");
    }

    [Fact]
    public async Task Rejects_a_start_after_the_end_of_the_video()
    {
        _downloader.Results.Enqueue(Audio(durationSeconds: 205));

        ClipOutOfRangeException error = await Assert.ThrowsAsync<ClipOutOfRangeException>(() => CreateClip(start: 210, end: 240));

        Assert.Equal(TimeSpan.FromSeconds(205), error.VideoDuration);
    }

    [Fact]
    public async Task Never_overwrites_an_existing_file()
    {
        Directory.CreateDirectory(_outputFolder);
        File.WriteAllText(Path.Combine(_outputFolder, "Song (1m35s-2m05s).mp3"), "older clip");
        _downloader.Results.Enqueue(Audio());

        ClipResult result = await CreateClip(start: 95, end: 125);

        Assert.Equal(Path.Combine(_outputFolder, "Song (1m35s-2m05s) (2).mp3"), result.OutputPath);
    }

    [Fact]
    public async Task Cancelling_removes_the_partial_file_and_the_temp_folder()
    {
        _downloader.Results.Enqueue(Audio());
        _converter.Throw = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateClip(start: 95, end: 125));

        Assert.Empty(Directory.GetFiles(_outputFolder));
        Assert.False(Directory.Exists(_downloader.LastTempDir));
    }

    [Fact]
    public async Task Removes_the_temp_folder_after_success()
    {
        _downloader.Results.Enqueue(Audio());

        await CreateClip(start: 95, end: 125);

        Assert.False(Directory.Exists(_downloader.LastTempDir));
    }

    [Fact]
    public async Task Creates_the_output_folder_before_downloading()
    {
        bool? existedDuringDownload = null;
        _downloader.OnDownload = () => existedDuringDownload = Directory.Exists(_outputFolder);
        _downloader.Results.Enqueue(Audio());

        await CreateClip(start: 95, end: 125);

        Assert.True(existedDuringDownload);
    }

    [Fact]
    public async Task Updates_and_retries_once_after_an_unknown_failure()
    {
        _downloader.Results.Enqueue(Fail(YtDlpFailure.Unknown));
        _downloader.Results.Enqueue(Audio());

        ClipResult result = await CreateClip(start: 95, end: 125);

        Assert.True(File.Exists(result.OutputPath));
        Assert.Equal(2, _downloader.DownloadCalls);
        Assert.Equal(1, _downloader.UpdateCalls);
    }

    [Fact]
    public async Task Does_not_retry_a_network_failure()
    {
        _downloader.Results.Enqueue(Fail(YtDlpFailure.Network));

        YtDlpException error = await Assert.ThrowsAsync<YtDlpException>(() => CreateClip(start: 95, end: 125));

        Assert.Equal(YtDlpFailure.Network, error.Failure);
        Assert.Equal(1, _downloader.DownloadCalls);
        Assert.Equal(0, _downloader.UpdateCalls);
    }

    private Task<ClipResult> CreateClip(double start, double end) =>
        new ClipService(_downloader, _converter).CreateClipAsync(
            new ClipRequest("https://youtu.be/x", TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end),
                Fade: true, FadeSeconds: 3, Normalize: true, OutputFolder: _outputFolder),
            progress: null,
            CancellationToken.None);

    private void AssertOption(string name, string value)
    {
        IReadOnlyList<string> args = _converter.Arguments!;
        int index = args.ToList().IndexOf(name);
        Assert.True(index >= 0, $"{name} missing");
        Assert.Equal(value, args[index + 1]);
    }

    private static Func<string, DownloadedAudio> Audio(double durationSeconds = 205) => tempDir =>
    {
        string path = Path.Combine(tempDir, "download.m4a");
        File.WriteAllText(path, "audio");
        return new DownloadedAudio(path, "Song", TimeSpan.FromSeconds(durationSeconds));
    };

    private static Func<string, DownloadedAudio> Fail(YtDlpFailure failure) =>
        _ => throw new YtDlpException(failure, "ERROR: simulated " + failure);

    private sealed class FakeDownloader : IAudioDownloader
    {
        public Queue<Func<string, DownloadedAudio>> Results { get; } = new();
        public int DownloadCalls { get; private set; }
        public int UpdateCalls { get; private set; }
        public string? LastTempDir { get; private set; }
        public Action? OnDownload { get; set; }

        public Task<DownloadedAudio> DownloadAudioAsync(string url, string tempDir, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            DownloadCalls++;
            LastTempDir = tempDir;
            OnDownload?.Invoke();
            return Task.FromResult(Results.Dequeue()(tempDir));
        }

        public Task<UpdateOutcome> UpdateAsync(CancellationToken cancellationToken)
        {
            UpdateCalls++;
            return Task.FromResult(new UpdateOutcome(true, "2026.09.01"));
        }
    }

    private sealed class FakeConverter : IAudioConverter
    {
        public IReadOnlyList<string>? Arguments { get; private set; }
        public Exception? Throw { get; set; }

        public Task ConvertAsync(IReadOnlyList<string> arguments, TimeSpan clipLength, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            Arguments = arguments;
            File.WriteAllText(arguments[^1], "mp3");
            if (Throw != null)
                throw Throw;
            return Task.CompletedTask;
        }
    }
}
