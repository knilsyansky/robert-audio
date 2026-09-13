namespace YtAudioDownloader.Tests;

public class YouTubeUrlTests
{
    [Theory]
    [InlineData("https://youtu.be/Ex-dEn5KAsY?t=95", 95)]
    [InlineData("https://youtu.be/Ex-dEn5KAsY?si=abc123&t=30", 30)]
    [InlineData("https://www.youtube.com/watch?v=Ex-dEn5KAsY&t=95s", 95)]
    [InlineData("https://www.youtube.com/watch?v=Ex-dEn5KAsY&t=1m35s", 95)]
    [InlineData("https://www.youtube.com/watch?v=Ex-dEn5KAsY&t=1h2m3s", 3723)]
    [InlineData("https://www.youtube.com/watch?v=Ex-dEn5KAsY#t=1m5s", 65)]
    [InlineData("https://www.youtube.com/embed/Ex-dEn5KAsY?start=42", 42)]
    public void TryGetStartTime_reads_the_shared_timestamp(string url, int expectedSeconds)
    {
        Assert.Equal<TimeSpan?>(TimeSpan.FromSeconds(expectedSeconds), YouTubeUrl.TryGetStartTime(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("https://www.youtube.com/watch?v=Ex-dEn5KAsY")]
    [InlineData("https://youtu.be/Ex-dEn5KAsY?t=")]
    [InlineData("https://youtu.be/Ex-dEn5KAsY?t=abc")]
    public void TryGetStartTime_returns_null_without_a_timestamp(string? url)
    {
        Assert.Null(YouTubeUrl.TryGetStartTime(url));
    }
}
