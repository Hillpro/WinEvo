using System.Resources;
using WinEvo.ActionModel;

namespace WinEvo.Actions;

/// <summary>
/// User-facing text of the built-in actions, backed by <c>Strings.resx</c>
/// (English) and its <c>Strings.&lt;lang&gt;.resx</c> translations.
/// </summary>
internal static class ActionText
{
    private static readonly ResourceManager s_strings = new(
        "WinEvo.Actions.Strings",
        typeof(ActionText).Assembly);

    public static LocalizableText For(string key) => LocalizableText.FromResource(s_strings, key);
}
