namespace YtAudioDownloader;

internal static class AppPaths
{
    // YTAUDIO_DATA_DIR lets tests and the packaged smoke test use a throwaway folder.
    public static string DataDir { get; } =
        Environment.GetEnvironmentVariable("YTAUDIO_DATA_DIR") is { Length: > 0 } overridden
            ? overridden
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YtAudioDownloader");

    public static string BundledToolsDir => Path.Combine(AppContext.BaseDirectory, "tools");
    public static string LogsDir => Path.Combine(DataDir, "logs");
    public static string TempRoot => Path.Combine(Path.GetTempPath(), "YtAudioDownloader");
    public static string DefaultOutputDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "YtAudioDownloader");
}
