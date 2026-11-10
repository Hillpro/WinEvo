using Microsoft.Win32;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Writes a registry value, creating the key if needed. Build one with the
/// factory matching the value's type:
/// <code>
/// RegistrySetOperation.Dword(@"HKCU\Software\...", "ValueName", 0)
/// </code>
/// The hive is the first path segment of the key. Supported forms (case-insensitive):
/// <c>HKCU</c>, <c>HKEY_CURRENT_USER</c>, <c>HKLM</c>, <c>HKEY_LOCAL_MACHINE</c>, <c>HKCR</c>,
/// <c>HKEY_CLASSES_ROOT</c>, <c>HKU</c>, <c>HKEY_USERS</c>, <c>HKCC</c>, <c>HKEY_CURRENT_CONFIG</c>.
/// </summary>
public sealed class RegistrySetOperation : ActionOperation
{
    private readonly object _data;

    private RegistrySetOperation(string key, string valueName, RegistryValueKind kind, object data)
    {
        Key = key;
        ValueName = valueName;
        Kind = kind;
        _data = data;
    }

    public override string Id => "registry-set";

    public string Key { get; }
    public string ValueName { get; }
    public RegistryValueKind Kind { get; }

    public static RegistrySetOperation Dword(string key, string valueName, int data)
        => new(key, valueName, RegistryValueKind.DWord, data);

    public static RegistrySetOperation Qword(string key, string valueName, long data)
        => new(key, valueName, RegistryValueKind.QWord, data);

    public static RegistrySetOperation Sz(string key, string valueName, string data)
        => new(key, valueName, RegistryValueKind.String, data);

    /// <summary>A string in which Windows expands <c>%VARIABLE%</c> references when it is read.</summary>
    public static RegistrySetOperation ExpandSz(string key, string valueName, string data)
        => new(key, valueName, RegistryValueKind.ExpandString, data);

    public static RegistrySetOperation MultiSz(string key, string valueName, IReadOnlyList<string> data)
        => new(key, valueName, RegistryValueKind.MultiString, data.ToArray());

    public override Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        try
        {
            var (hiveName, subkeyPath) = RegistryPath.SplitHive(Key);
            var root = RegistryPath.ResolveHive(hiveName);
            if (root is null)
                return Task.FromResult(OperationResult.Fail($"unknown hive '{hiveName}' in key path '{Key}'"));

            using var key = root.CreateSubKey(subkeyPath, writable: true)
                ?? throw new InvalidOperationException($"unable to open key '{subkeyPath}'");

            key.SetValue(ValueName, _data, Kind);

            var written = _data is string[] lines ? string.Join(", ", lines) : _data;
            context.Log($"set {hiveName}\\{subkeyPath}\\{ValueName} = {written} ({Kind})");
            return Task.FromResult(OperationResult.Ok($"{hiveName}\\{subkeyPath}\\{ValueName} = {written}"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(OperationResult.Fail("access denied (elevation may be required)", ex.Message));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult.Fail(ex.GetType().Name, ex.Message));
        }
    }
}
