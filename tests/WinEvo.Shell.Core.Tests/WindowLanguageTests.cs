using System.Globalization;

namespace WinEvo.Shell.Core.Tests;

/// <summary>
/// The window is shown in exactly one of its translations, whatever language
/// Windows asks for.
/// </summary>
public class WindowLanguageTests
{
    [Theory]
    [InlineData("fr")]
    [InlineData("fr-FR")]
    [InlineData("fr-CA")]
    [InlineData("fr-BE")]
    public void Any_French_is_shown_in_French(string requested)
    {
        Assert.Equal("fr", WindowLanguage.Choose(CultureInfo.GetCultureInfo(requested)).Name);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("en-GB")]
    [InlineData("en-CA")]
    public void Any_English_is_shown_in_English(string requested)
    {
        Assert.Equal("en", WindowLanguage.Choose(CultureInfo.GetCultureInfo(requested)).Name);
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("ja-JP")]
    [InlineData("es")]
    public void A_language_without_a_translation_is_shown_in_English(string requested)
    {
        Assert.Equal("en", WindowLanguage.Choose(CultureInfo.GetCultureInfo(requested)).Name);
    }

    [Fact]
    public void The_invariant_culture_is_shown_in_English()
    {
        Assert.Equal("en", WindowLanguage.Choose(CultureInfo.InvariantCulture).Name);
    }
}
