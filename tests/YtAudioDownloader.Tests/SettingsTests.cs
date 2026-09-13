namespace YtAudioDownloader.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "YtAudioDownloader.Tests", Guid.NewGuid().ToString("N"));
    private string SettingsFile => Path.Combine(_folder, "settings.json");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Saved_settings_load_back()
    {
        new Settings { OutputFolder = @"D:\Музыка" }.Save(SettingsFile);

        Assert.Equal(@"D:\Музыка", Settings.Load(SettingsFile).OutputFolder);
    }

    [Fact]
    public void Missing_file_gives_defaults()
    {
        Assert.Null(Settings.Load(SettingsFile).OutputFolder);
    }

    [Fact]
    public void Corrupt_file_gives_defaults()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsFile, "{ not json");

        Assert.Null(Settings.Load(SettingsFile).OutputFolder);
    }
}
