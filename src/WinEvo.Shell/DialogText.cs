using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace WinEvo.Shell;

/// <summary>
/// Text the Shell sets from code instead of XAML (dialog buttons, severity
/// labels, prompts), read from <c>Strings\&lt;language&gt;\Resources.resw</c>.
/// Text that sits in XAML is localized in place through <c>x:Uid</c> and does
/// not come through here.
/// </summary>
internal static class DialogText
{
    private static readonly ResourceLoader s_resources = new();

    /// <summary>An unknown key resolves to the key itself, so the mistake is visible in the UI instead of blank.</summary>
    public static string Get(string key)
    {
        var text = s_resources.GetString(key);
        return string.IsNullOrEmpty(text) ? key : text;
    }

    public static string Format(string key, params object?[] arguments)
        => string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
}
