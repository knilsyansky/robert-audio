using System.Runtime.CompilerServices;

namespace YtAudioDownloader.Tests;

internal static class TestEnvironment
{
    // Keeps logs and settings written during tests out of the real %LOCALAPPDATA%.
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize() =>
        Environment.SetEnvironmentVariable("YTAUDIO_DATA_DIR", Path.Combine(Path.GetTempPath(), "YtAudioDownloader.Tests", "data"));
}
