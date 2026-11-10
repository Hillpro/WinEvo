using System.Globalization;
using System.Resources;

namespace WinEvo.Shell.Core;

/// <summary>
/// Text produced by the view models and services (status lines, the footer,
/// drive labels), read from <c>Strings.resx</c> and its
/// <c>Strings.&lt;language&gt;.resx</c> translations.
/// </summary>
internal static class ViewModelText
{
    private static readonly ResourceManager s_strings = new(
        "WinEvo.Shell.Core.Strings",
        typeof(ViewModelText).Assembly);

    /// <summary>An unknown key resolves to the key itself, so the mistake is visible in the UI instead of fatal.</summary>
    public static string Get(string key, CultureInfo culture) => Find(key, culture) ?? key;

    /// <summary>The text for <paramref name="key"/>, or <see langword="null"/> when no such key exists.</summary>
    public static string? Find(string key, CultureInfo culture) => s_strings.GetString(key, culture);

    public static string Format(string key, CultureInfo culture, params object?[] arguments)
        => string.Format(culture, Get(key, culture), arguments);
}
