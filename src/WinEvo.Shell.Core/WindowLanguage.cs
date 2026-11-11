using System.Globalization;

namespace WinEvo.Shell.Core;

/// <summary>
/// The languages the window is translated into, and the one it is shown in.
/// Every piece of text is read in that single language, so the window never
/// mixes two.
/// </summary>
public static class WindowLanguage
{
    /// <summary>Shown when the requested language has no translation. Matches <c>DefaultLanguage</c> in the Shell's csproj.</summary>
    public const string Fallback = "en";

    /// <summary>Neutral language tags the window is translated into, one per <c>Strings\&lt;tag&gt;</c> folder of the Shell.</summary>
    public static IReadOnlyList<string> Translated { get; } = [Fallback, "fr"];

    /// <summary>
    /// The translated language for <paramref name="requested"/>, matched on its
    /// neutral language (<c>fr-CA</c> gives <c>fr</c>), or <see cref="Fallback"/>.
    /// </summary>
    public static CultureInfo Choose(CultureInfo requested)
    {
        var neutral = requested.TwoLetterISOLanguageName;
        return CultureInfo.GetCultureInfo(Translated.Contains(neutral) ? neutral : Fallback);
    }
}
