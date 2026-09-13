using System.Reflection;

namespace YtAudioDownloader.Tests;

public class StringsTests
{
    private static readonly PropertyInfo[] Texts = typeof(UiText).GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.PropertyType == typeof(string))
        .ToArray();

    [Theory]
    [InlineData("ru", true)]
    [InlineData("RU", true)]
    [InlineData("en", false)]
    [InlineData("de", false)]
    public void ForLanguage_picks_russian_only_for_russian(string code, bool russian)
    {
        Assert.Same(russian ? Strings.Russian : Strings.English, Strings.ForLanguage(code));
    }

    [Fact]
    public void Every_text_is_filled_in_both_languages()
    {
        Assert.NotEmpty(Texts);
        foreach (PropertyInfo property in Texts)
        {
            Assert.False(string.IsNullOrWhiteSpace((string?)property.GetValue(Strings.English)), "English " + property.Name);
            Assert.False(string.IsNullOrWhiteSpace((string?)property.GetValue(Strings.Russian)), "Russian " + property.Name);
        }
    }

    [Fact]
    public void Placeholders_match_between_languages()
    {
        foreach (PropertyInfo property in Texts)
        {
            bool english = ((string)property.GetValue(Strings.English)!).Contains("{0}");
            bool russian = ((string)property.GetValue(Strings.Russian)!).Contains("{0}");
            Assert.True(english == russian, property.Name);
        }
    }
}
