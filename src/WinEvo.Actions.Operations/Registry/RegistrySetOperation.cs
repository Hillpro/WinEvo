using System.Text.Json;
using Microsoft.Win32;
using WinEvo.ActionModel;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Writes a registry value. Manifest shape:
/// <code>
/// { "operation": "registry-set",
///   "key": "HKCU\\Software\\...",
///   "value": "ValueName",
///   "type": "DWORD",
///   "data": 0,
///   "backupForUndo": true }   // TODO: undo not wired yet
/// </code>
/// The hive is the first path segment of <c>key</c>. Supported forms (case-insensitive):
/// <c>HKCU</c>, <c>HKEY_CURRENT_USER</c>, <c>HKLM</c>, <c>HKEY_LOCAL_MACHINE</c>, <c>HKCR</c>,
/// <c>HKEY_CLASSES_ROOT</c>, <c>HKU</c>, <c>HKEY_USERS</c>, <c>HKCC</c>, <c>HKEY_CURRENT_CONFIG</c>.
/// </summary>
public sealed class RegistrySetOperation : IActionOperation
{
    private static readonly char[] s_pathSeparators = ['\\', '/'];

    public string Id => "registry-set";

    public Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        try
        {
            var keyPath = context.RenderProperty("key");
            var valueName = context.RenderProperty("value");
            var type = context.RenderProperty("type").ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(keyPath))
                return Task.FromResult(OperationResult.Fail("missing 'key' property"));

            if (!context.Step.Properties.TryGetProperty("data", out var dataElement))
                return Task.FromResult(OperationResult.Fail("missing 'data' property"));

            var (hiveName, subkeyPath) = SplitHive(keyPath);
            var root = ResolveHive(hiveName);
            if (root is null)
                return Task.FromResult(OperationResult.Fail($"unknown hive '{hiveName}' in key path '{keyPath}'"));

            using var key = root.CreateSubKey(subkeyPath, writable: true)
                ?? throw new InvalidOperationException($"unable to open key '{subkeyPath}'");

            var (value, kind) = ConvertData(dataElement, type, context.Parameters);
            key.SetValue(valueName, value, kind);

            context.Log($"set {hiveName}\\{subkeyPath}\\{valueName} = {value} ({kind})");
            return Task.FromResult(OperationResult.Ok($"{hiveName}\\{subkeyPath}\\{valueName} = {value}"));
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

    /// <summary>
    /// Splits a full registry path (e.g. <c>"HKCU\Software\Foo"</c>) into the hive
    /// identifier and the remainder. If the path contains no separator, the entire
    /// string is treated as the hive and the subkey is empty.
    /// </summary>
    private static (string hive, string subkey) SplitHive(string keyPath)
    {
        var separatorIndex = keyPath.IndexOfAny(s_pathSeparators);
        if (separatorIndex < 0)
            return (keyPath.Trim(), string.Empty);
        return (keyPath[..separatorIndex].Trim(), keyPath[(separatorIndex + 1)..].Trim());
    }

    private static RegistryKey? ResolveHive(string hive) => hive.ToUpperInvariant() switch
    {
        "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
        "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
        "HKCR" or "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
        "HKU" or "HKEY_USERS" => Registry.Users,
        "HKCC" or "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
        _ => null,
    };

    private static (object value, RegistryValueKind kind) ConvertData(
        JsonElement data, string type, IReadOnlyDictionary<string, object?> parameters)
    {
        return type switch
        {
            "DWORD" => (ReadInt32(data, parameters), RegistryValueKind.DWord),
            "QWORD" => (ReadInt64(data, parameters), RegistryValueKind.QWord),
            "STRING" or "REG_SZ" => (ReadString(data, parameters), RegistryValueKind.String),
            "EXPAND_STRING" or "REG_EXPAND_SZ" => (ReadString(data, parameters), RegistryValueKind.ExpandString),
            "MULTI_STRING" or "REG_MULTI_SZ" => (ReadStringArray(data, parameters), RegistryValueKind.MultiString),
            _ => throw new InvalidOperationException($"unsupported type '{type}'"),
        };

    }

    private static int ReadInt32(JsonElement data, IReadOnlyDictionary<string, object?> parameters) => data.ValueKind switch
    {
        JsonValueKind.Number => data.GetInt32(),
        JsonValueKind.String => int.Parse(Templating.Render(data.GetString()!, parameters), System.Globalization.CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException("DWORD data must be number or templated string"),
    };

    private static long ReadInt64(JsonElement data, IReadOnlyDictionary<string, object?> parameters) => data.ValueKind switch
    {
        JsonValueKind.Number => data.GetInt64(),
        JsonValueKind.String => long.Parse(Templating.Render(data.GetString()!, parameters), System.Globalization.CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException("QWORD data must be number or templated string"),
    };

    private static string ReadString(JsonElement data, IReadOnlyDictionary<string, object?> parameters)
        => data.ValueKind == JsonValueKind.String
            ? Templating.Render(data.GetString() ?? "", parameters)
            : data.GetRawText();

    private static string[] ReadStringArray(JsonElement data, IReadOnlyDictionary<string, object?> parameters)
    {
        if (data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("MULTI_STRING data must be an array of strings");
        return data.EnumerateArray()
            .Select(e => Templating.Render(e.GetString() ?? "", parameters))
            .ToArray();
    }
}
