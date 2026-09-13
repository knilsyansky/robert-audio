namespace YtAudioDownloader.Tests;

public class OutputNamingTests
{
    [Theory]
    [InlineData("Crypt of The Seetherdancer", "Crypt of The Seetherdancer")]
    [InlineData("AC/DC: Back In Black?", "AC DC Back In Black")]
    [InlineData("Песня — тест", "Песня — тест")]
    [InlineData("a\tb\nc", "a b c")]
    [InlineData("  spaced   out  ", "spaced out")]
    [InlineData("<>:\"/\\|?*", "audio")]
    [InlineData("", "audio")]
    [InlineData(null, "audio")]
    public void SanitizeTitle_keeps_only_what_windows_allows(string? title, string expected)
    {
        Assert.Equal(expected, OutputNaming.SanitizeTitle(title));
    }

    [Fact]
    public void SanitizeTitle_limits_length_to_120()
    {
        Assert.Equal(new string('a', 120), OutputNaming.SanitizeTitle(new string('a', 200)));
    }

    [Fact]
    public void SanitizeTitle_does_not_cut_an_emoji_in_half()
    {
        string title = new string('a', 119) + "😀" + "tail";

        Assert.Equal(new string('a', 119), OutputNaming.SanitizeTitle(title));
    }

    [Fact]
    public void BuildFileName_appends_the_clip_range()
    {
        Assert.Equal(
            "Crypt of The Seetherdancer (1m35s-2m05s).mp3",
            OutputNaming.BuildFileName("Crypt of The Seetherdancer", TimeSpan.FromSeconds(95), TimeSpan.FromSeconds(125)));
    }

    [Fact]
    public void GetAvailablePath_returns_the_plain_name_when_free()
    {
        Assert.Equal(@"C:\music\a.mp3", OutputNaming.GetAvailablePath(@"C:\music", "a.mp3", _ => false));
    }

    [Fact]
    public void GetAvailablePath_never_overwrites_existing_files()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"C:\music\a.mp3", @"C:\music\a (2).mp3" };

        Assert.Equal(@"C:\music\a (3).mp3", OutputNaming.GetAvailablePath(@"C:\music", "a.mp3", existing.Contains));
    }
}
