namespace YtAudioDownloader.Tests;

public class HeadlessClipTests
{
    [Fact]
    public void TryParseArguments_builds_a_request_with_default_options()
    {
        Assert.True(HeadlessClip.TryParseArguments(new[] { "https://youtu.be/x", "1:00", "1:30", @"C:\out" }, out ClipRequest? request, out _));

        Assert.Equal(new ClipRequest("https://youtu.be/x", TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(90), true, 3, true, @"C:\out"), request);
    }

    [Theory]
    [InlineData("https://youtu.be/x", "1:00", "1:30")]
    [InlineData("https://youtu.be/x", "abc", "1:30", @"C:\out")]
    [InlineData("https://youtu.be/x", "1:30", "1:00", @"C:\out")]
    public void TryParseArguments_rejects_bad_input(params string[] args)
    {
        Assert.False(HeadlessClip.TryParseArguments(args, out _, out string? error));
        Assert.False(string.IsNullOrEmpty(error));
    }
}
