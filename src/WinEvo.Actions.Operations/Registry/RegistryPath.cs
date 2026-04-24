using Microsoft.Win32;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Shared registry-path parsing used by every registry-* operation.
/// The hive is the first path segment of the input (<c>HKCU\Software\...</c>);
/// the remainder is the subkey path.
/// </summary>
internal static class RegistryPath
{
    private static readonly char[] s_separators = ['\\', '/'];

    /// <summary>
    /// Splits a full registry path (e.g. <c>"HKCU\Software\Foo"</c>) into the
    /// hive identifier and the subkey path. If no separator is present, the
    /// whole string is treated as the hive and the subkey is empty.
    /// </summary>
    public static (string hive, string subkey) SplitHive(string keyPath)
    {
        var index = keyPath.IndexOfAny(s_separators);
        if (index < 0)
            return (keyPath.Trim(), string.Empty);
        return (keyPath[..index].Trim(), keyPath[(index + 1)..].Trim());
    }

    /// <summary>
    /// Resolves a hive identifier (short or long form, case-insensitive) to
    /// its <see cref="RegistryKey"/> root. Returns <see langword="null"/>
    /// for unknown identifiers.
    /// </summary>
    public static RegistryKey? ResolveHive(string hive) => hive.ToUpperInvariant() switch
    {
        "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
        "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
        "HKCR" or "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
        "HKU" or "HKEY_USERS" => Registry.Users,
        "HKCC" or "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
        _ => null,
    };
}
