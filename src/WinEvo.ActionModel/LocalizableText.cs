using System.Globalization;
using System.Resources;

namespace WinEvo.ActionModel;

/// <summary>
/// User-facing text that is resolved to a concrete string only when the
/// display culture is known. Descriptors carry these instead of strings so
/// the same descriptor serves every language.
/// </summary>
public abstract class LocalizableText
{
    public abstract string Resolve(CultureInfo culture);

    /// <summary>Text that reads the same in every culture.</summary>
    public static LocalizableText Literal(string text) => new LiteralText(text);

    /// <summary>
    /// Text looked up in a <c>.resx</c> resource set, with the standard
    /// culture fallback (<c>fr-CA</c> → <c>fr</c> → neutral). An unknown
    /// <paramref name="key"/> resolves to the key itself, so the mistake is
    /// visible in the UI instead of fatal.
    /// </summary>
    public static LocalizableText FromResource(ResourceManager resources, string key)
        => new ResourceText(resources, key);

    private sealed class LiteralText(string text) : LocalizableText
    {
        public override string Resolve(CultureInfo culture) => text;
    }

    private sealed class ResourceText(ResourceManager resources, string key) : LocalizableText
    {
        public override string Resolve(CultureInfo culture) => resources.GetString(key, culture) ?? key;
    }
}
