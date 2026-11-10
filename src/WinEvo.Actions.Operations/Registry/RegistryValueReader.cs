namespace WinEvo.Actions.Operations;

/// <summary>
/// Reads registry values for actions that mirror system state. Key paths
/// follow the same rules as <see cref="RegistrySetOperation"/>: the hive is
/// the first path segment (<c>HKCU\Software\...</c>).
/// </summary>
public static class RegistryValueReader
{
    /// <summary>
    /// The value's data in its native CLR type (<see cref="int"/> for DWORD,
    /// <see cref="long"/> for QWORD, <see cref="string"/>, <c>string[]</c>,
    /// <c>byte[]</c>), or <see langword="null"/> when the key or the value
    /// does not exist.
    /// </summary>
    /// <exception cref="ArgumentException">The path does not start with a known hive.</exception>
    public static object? Read(string keyPath, string valueName)
    {
        var (hiveName, subkeyPath) = RegistryPath.SplitHive(keyPath);
        var root = RegistryPath.ResolveHive(hiveName)
            ?? throw new ArgumentException($"unknown hive '{hiveName}' in key path '{keyPath}'", nameof(keyPath));

        using var key = root.OpenSubKey(subkeyPath);
        return key?.GetValue(valueName, defaultValue: null);
    }
}
