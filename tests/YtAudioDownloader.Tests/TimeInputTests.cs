using System.Globalization;

namespace YtAudioDownloader.Tests;

public class TimeInputTests
{
    [Theory]
    [InlineData("95", 95.0)]
    [InlineData("95.5", 95.5)]
    [InlineData("0", 0.0)]
    [InlineData("1:35", 95.0)]
    [InlineData("01:35", 95.0)]
    [InlineData("1:5", 65.0)]
    [InlineData("1:35.5", 95.5)]
    [InlineData("1:35,5", 95.5)]
    [InlineData("0:01:35", 95.0)]
    [InlineData("1:02:03", 3723.0)]
    [InlineData("1:02:03.25", 3723.25)]
    [InlineData("75:00", 4500.0)]
    [InlineData("  1:35  ", 95.0)]
    public void TryParse_accepts_human_time_formats(string text, double expectedSeconds)
    {
        Assert.True(TimeInput.TryParse(text, out TimeSpan value));
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("5s")]
    [InlineData("-5")]
    [InlineData("1:60")]
    [InlineData("1:61:00")]
    [InlineData("1:-5")]
    [InlineData("1::5")]
    [InlineData(":5")]
    [InlineData("1:")]
    [InlineData("1.5:30")]
    [InlineData("1:2:3:4")]
    [InlineData("100:00:00")]
    [InlineData("NaN")]
    [InlineData("1:NaN")]
    [InlineData("1:02:NaN")]
    [InlineData("Infinity")]
    public void TryParse_rejects_invalid_input(string? text)
    {
        Assert.False(TimeInput.TryParse(text, out _));
    }

    [Theory]
    [InlineData(95.0, "1m35s")]
    [InlineData(5.0, "0m05s")]
    [InlineData(125.0, "2m05s")]
    [InlineData(95.5, "1m35.5s")]
    [InlineData(95.25, "1m35.25s")]
    [InlineData(3723.0, "1h02m03s")]
    [InlineData(0.0, "0m00s")]
    public void FormatCompact_writes_file_name_friendly_times(double seconds, string expected)
    {
        Assert.Equal(expected, TimeInput.FormatCompact(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(95.0, "1:35")]
    [InlineData(5.0, "0:05")]
    [InlineData(125.0, "2:05")]
    [InlineData(95.5, "1:35.5")]
    [InlineData(3723.0, "1:02:03")]
    public void FormatDisplay_writes_what_TryParse_reads(double seconds, string expected)
    {
        TimeSpan time = TimeSpan.FromSeconds(seconds);
        string text = TimeInput.FormatDisplay(time);

        Assert.Equal(expected, text);
        Assert.True(TimeInput.TryParse(text, out TimeSpan roundTrip));
        Assert.Equal(time, roundTrip);
    }

    [Fact]
    public void Formatting_uses_a_dot_even_on_russian_windows()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
        try
        {
            Assert.Equal("1:35.5", TimeInput.FormatDisplay(TimeSpan.FromSeconds(95.5)));
            Assert.Equal("1m35.5s", TimeInput.FormatCompact(TimeSpan.FromSeconds(95.5)));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
