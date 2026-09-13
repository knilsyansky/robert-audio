namespace YtAudioDownloader.Tests;

public class ProjectReferenceTests
{
    [Fact]
    public void Tests_reference_the_app_assembly()
    {
        Assert.Equal("YtAudioDownloader", typeof(MainForm).Assembly.GetName().Name);
    }
}
