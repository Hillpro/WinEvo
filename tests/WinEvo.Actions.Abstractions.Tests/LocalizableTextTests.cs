using System.Globalization;
using System.Resources;

namespace WinEvo.Actions.Abstractions.Tests;

/// <summary>
/// Resolves against <c>LocalizableTextTestStrings.resx</c> (neutral, English)
/// and its <c>.fr.resx</c> satellite, so the culture fallback is exercised
/// through the same resource pipeline the shipped actions use.
/// </summary>
public class LocalizableTextTests
{
    private static readonly ResourceManager s_resources = new(
        "WinEvo.Actions.Abstractions.Tests.LocalizableTextTestStrings",
        typeof(LocalizableTextTests).Assembly);

    [Fact]
    public void Literal_reads_the_same_in_every_culture()
    {
        var text = LocalizableText.Literal("WinEvo");

        Assert.Equal("WinEvo", text.Resolve(Culture("fr-FR")));
        Assert.Equal("WinEvo", text.Resolve(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Resource_resolves_in_the_requested_language()
    {
        var text = LocalizableText.FromResource(s_resources, "Greeting");

        Assert.Equal("Bonjour", text.Resolve(Culture("fr")));
    }

    [Fact]
    public void Regional_culture_falls_back_to_its_language()
    {
        var text = LocalizableText.FromResource(s_resources, "Greeting");

        Assert.Equal("Bonjour", text.Resolve(Culture("fr-CA")));
    }

    [Fact]
    public void Untranslated_language_falls_back_to_english()
    {
        var text = LocalizableText.FromResource(s_resources, "Greeting");

        Assert.Equal("Hello", text.Resolve(Culture("de-DE")));
    }

    [Fact]
    public void Key_missing_from_a_translation_falls_back_to_english()
    {
        var text = LocalizableText.FromResource(s_resources, "OnlyInEnglish");

        Assert.Equal("Not translated", text.Resolve(Culture("fr-FR")));
    }

    [Fact]
    public void Unknown_key_resolves_to_the_key_itself()
    {
        var text = LocalizableText.FromResource(s_resources, "NoSuchKey");

        Assert.Equal("NoSuchKey", text.Resolve(Culture("fr-FR")));
    }

    private static CultureInfo Culture(string name) => CultureInfo.GetCultureInfo(name);
}
