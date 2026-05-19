using System.Globalization;
using System.Text.Json;
using Microsoft.Win32;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Reads a registry value. Manifest shape:
/// <code>
/// { "operation": "registry-read",
///   "key": "HKCU\\Software\\...",
///   "value": "ValueName" }
/// </code>
/// The hive is derived from the first path segment of <c>key</c> (same rules as
/// <see cref="RegistrySetOperation"/>). On success, <see cref="OperationResult.Message"/>
/// is a small JSON object — <c>{"present":true,"kind":"DWord","data":1}</c> — so
/// callers can parse without having to know the value's native CLR type. Missing
/// keys or values are reported as <c>{"present":false}</c>, not as failures.
/// </summary>
public sealed class RegistryReadOperation : ActionOperation
{
    public override string Id => "registry-read";

    public required string Key { get; init; }
    public required string Value { get; init; }

    public static RegistryReadOperation FromJson(JsonElement properties)
    {
        return new RegistryReadOperation
        {
            Key = RequireString(properties, "key"),
            Value = RequireString(properties, "value"),
        };
    }

    public override Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        try
        {
            var keyPath = RenderProperty(Key, context);
            var valueName = RenderProperty(Value, context);

            if (string.IsNullOrWhiteSpace(keyPath))
                return Task.FromResult(OperationResult.Fail("missing 'key' property"));

            var (hiveName, subkeyPath) = RegistryPath.SplitHive(keyPath);
            var root = RegistryPath.ResolveHive(hiveName);
            if (root is null)
                return Task.FromResult(OperationResult.Fail($"unknown hive '{hiveName}' in key path '{keyPath}'"));

            using var key = root.OpenSubKey(subkeyPath);
            if (key is null)
            {
                context.Log($"key {hiveName}\\{subkeyPath} not present");
                return Task.FromResult(OperationResult.Ok(SerializeMissing()));
            }

            var raw = key.GetValue(valueName, defaultValue: null);
            if (raw is null)
            {
                context.Log($"value {hiveName}\\{subkeyPath}\\{valueName} not present");
                return Task.FromResult(OperationResult.Ok(SerializeMissing()));
            }

            var kind = key.GetValueKind(valueName);
            context.Log($"read {hiveName}\\{subkeyPath}\\{valueName} = {raw} ({kind})");
            return Task.FromResult(OperationResult.Ok(SerializePresent(kind, raw)));
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
            throw new JsonException($"registry-read: missing or non-string '{property}' property");
        return value.GetString()!;
    }

    private static string SerializeMissing()
        => """{"present":false}""";

    private static string SerializePresent(RegistryValueKind kind, object raw)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("present", true);
            writer.WriteString("kind", kind.ToString());
            writer.WritePropertyName("data");
            WriteData(writer, kind, raw);
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteData(Utf8JsonWriter writer, RegistryValueKind kind, object raw)
    {
        switch (kind)
        {
            case RegistryValueKind.DWord:
                writer.WriteNumberValue(Convert.ToInt32(raw, CultureInfo.InvariantCulture));
                break;
            case RegistryValueKind.QWord:
                writer.WriteNumberValue(Convert.ToInt64(raw, CultureInfo.InvariantCulture));
                break;
            case RegistryValueKind.MultiString:
                writer.WriteStartArray();
                foreach (var s in (string[])raw)
                    writer.WriteStringValue(s);
                writer.WriteEndArray();
                break;
            case RegistryValueKind.Binary:
                writer.WriteBase64StringValue((byte[])raw);
                break;
            default:
                writer.WriteStringValue(raw.ToString() ?? "");
                break;
        }
    }
}
