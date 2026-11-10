using System.Collections;
using System.Globalization;
using System.Resources;
using WinEvo.ActionModel;

namespace WinEvo.Actions.Tests;

/// <summary>
/// A <see cref="LocalizableText"/> whose key is missing resolves to the key
/// itself instead of failing, so a typo or a forgotten translation would only
/// show up in the UI. These tests catch it at build time by checking every
/// text of every action against the strings actually present in the
/// resources.
/// </summary>
public class ActionTextTests
{
    private static readonly CultureInfo s_french = CultureInfo.GetCultureInfo("fr");

    private static readonly ResourceManager s_strings = new(
        "WinEvo.Actions.Strings",
        typeof(ShippedActionCatalog).Assembly);

    [Fact]
    public void Every_action_text_resolves_in_english()
    {
        var english = StringsDeclaredFor(CultureInfo.InvariantCulture).Values.ToHashSet();

        Assert.All(AllActionTexts(), text => Assert.Contains(text.Resolve(CultureInfo.InvariantCulture), english));
    }

    [Fact]
    public void Every_action_text_resolves_in_french()
    {
        var french = StringsDeclaredFor(s_french).Values.ToHashSet();

        Assert.All(AllActionTexts(), text => Assert.Contains(text.Resolve(s_french), french));
    }

    [Fact]
    public void French_translates_exactly_the_english_keys()
    {
        var englishKeys = StringsDeclaredFor(CultureInfo.InvariantCulture).Keys.Order();
        var frenchKeys = StringsDeclaredFor(s_french).Keys.Order();

        Assert.Equal(englishKeys, frenchKeys);
    }

    private static IEnumerable<LocalizableText> AllActionTexts()
    {
        foreach (var action in new ShippedActionCatalog().Actions)
        {
            var descriptor = action.Descriptor;
            yield return descriptor.Name;
            if (descriptor.Description is not null)
                yield return descriptor.Description;

            foreach (var parameter in descriptor.Parameters)
            {
                if (parameter.Name is not null)
                    yield return parameter.Name;
                if (parameter.Description is not null)
                    yield return parameter.Description;
            }
        }
    }

    /// <summary>The strings declared in exactly this culture's resource file, without fallback to a parent culture.</summary>
    private static Dictionary<string, string> StringsDeclaredFor(CultureInfo culture)
    {
        var declared = s_strings.GetResourceSet(culture, createIfNotExists: true, tryParents: false)
            ?? throw new InvalidOperationException($"no resource file for culture '{culture.Name}'");

        return declared.Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
    }
}
