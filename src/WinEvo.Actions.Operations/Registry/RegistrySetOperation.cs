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
///   "data": 0 }
/// </code>
/// The hive is the first path segment of <c>key</c>. Supported forms (case-insensitive):
/// <c>HKCU</c>, <c>HKEY_CURRENT_USER</c>, <c>HKLM</c>, <c>HKEY_LOCAL_MACHINE</c>, <c>HKCR</c>,
/// <c>HKEY_CLASSES_ROOT</c>, <c>HKU</c>, <c>HKEY_USERS</c>, <c>HKCC</c>, <c>HKEY_CURRENT_CONFIG</c>.
/// </summary>
public sealed class RegistrySetOperation : ActionOperation
{
    public override string Id => "registry-set";

    public required string Key { get; init; }
    public required string Value { get; init; }
    public required string DataType { get; init; }

    /// <summary>Raw JSON of the value to write; interpreted according to <see cref="DataType"/>.</summary>
    public required JsonElement Data { get; init; }

    public static RegistrySetOperation FromJson(JsonElement properties)
    {
        if (!properties.TryGetProperty("data", out var data))
            throw new JsonException("registry-set: missing 'data' property");
        return new RegistrySetOperation
        {
            Key = RequireString(properties, "key"),
            Value = RequireString(properties, "value"),
            DataType = RequireString(properties, "type"),
            Data = data.Clone(),
        };
    }

    public override Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        try
        {
            var keyPath = RenderProperty(Key, context);
            var valueName = RenderProperty(Value, context);
            var type = RenderProperty(DataType, context).ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(keyPath))
                return Task.FromResult(OperationResult.Fail("missing 'key' property"));

            var (hiveName, subkeyPath) = RegistryPath.SplitHive(keyPath);
            var root = RegistryPath.ResolveHive(hiveName);
            if (root is null)
                return Task.FromResult(OperationResult.Fail($"unknown hive '{hiveName}' in key path '{keyPath}'"));

            using var key = root.CreateSubKey(subkeyPath, writable: true)
                ?? throw new InvalidOperationException($"unable to open key '{subkeyPath}'");

            var (value, kind) = ConvertData(Data, type, context.Parameters);
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

    private static string RequireString(JsonElement el, string property)
    {
        if (!el.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
            throw new JsonException($"registry-set: missing or non-string '{property}' property");
        return value.GetString()!;
    }

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
        JsonValueKind.True => 1,
        JsonValueKind.False => 0,
        JsonValueKind.String => ParseInt32(Templating.Render(data.GetString()!, parameters)),
        _ => throw new InvalidOperationException("DWORD data must be number, boolean or templated string"),
    };

    private static int ParseInt32(string rendered) => rendered switch
    {
        "true" => 1,
        "false" => 0,
        _ => int.Parse(rendered, System.Globalization.CultureInfo.InvariantCulture),
    };

    private static long ReadInt64(JsonElement data, IReadOnlyDictionary<string, object?> parameters) => data.ValueKind switch
    {
        JsonValueKind.Number => data.GetInt64(),
        JsonValueKind.True => 1,
        JsonValueKind.False => 0,
        JsonValueKind.String => ParseInt64(Templating.Render(data.GetString()!, parameters)),
        _ => throw new InvalidOperationException("QWORD data must be number, boolean or templated string"),
    };

    private static long ParseInt64(string rendered) => rendered switch
    {
        "true" => 1,
        "false" => 0,
        _ => long.Parse(rendered, System.Globalization.CultureInfo.InvariantCulture),
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
