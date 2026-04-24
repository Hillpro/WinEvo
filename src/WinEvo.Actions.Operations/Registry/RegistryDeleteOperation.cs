using System.Text.Json;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Deletes a registry value or a registry key (subtree). Manifest shapes:
/// <code>
/// // Delete a single value:
/// { "operation": "registry-delete",
///   "key": "HKCU\\Software\\Foo",
///   "value": "MyValue" }
///
/// // Delete a key and everything under it:
/// { "operation": "registry-delete",
///   "key": "HKCU\\Software\\Foo" }
/// </code>
/// When <see cref="ValueName"/> is present (even as an empty string — the
/// default-value slot), only that value is removed. When it's <see langword="null"/>,
/// the whole subkey tree under <see cref="Key"/> is removed. Idempotent:
/// deleting something that is already absent succeeds.
/// </summary>
public sealed class RegistryDeleteOperation : ActionOperation
{
    public override string Id => "registry-delete";

    public required string Key { get; init; }

    /// <summary>Name of the value to delete. <see langword="null"/> means "delete the whole key tree."</summary>
    public string? ValueName { get; init; }

    public static RegistryDeleteOperation FromJson(JsonElement properties)
    {
        if (!properties.TryGetProperty("key", out var keyProp) || keyProp.ValueKind != JsonValueKind.String)
            throw new JsonException("registry-delete: missing or non-string 'key' property");
        return new RegistryDeleteOperation
        {
            Key = keyProp.GetString()!,
            ValueName = properties.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null,
        };
    }

    public override Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        try
        {
            var keyPath = RenderProperty(Key, context);
            if (string.IsNullOrWhiteSpace(keyPath))
                return Task.FromResult(OperationResult.Fail("missing 'key' property"));

            var (hiveName, subkeyPath) = RegistryPath.SplitHive(keyPath);
            var root = RegistryPath.ResolveHive(hiveName);
            if (root is null)
                return Task.FromResult(OperationResult.Fail($"unknown hive '{hiveName}' in key path '{keyPath}'"));

            if (ValueName is not null)
            {
                var valueName = RenderProperty(ValueName, context);
                if (string.IsNullOrEmpty(subkeyPath))
                    return Task.FromResult(OperationResult.Fail($"cannot delete values from hive root '{hiveName}'"));

                using var key = root.OpenSubKey(subkeyPath, writable: true);
                if (key is null)
                {
                    context.Log($"key {hiveName}\\{subkeyPath} not present; nothing to delete");
                    return Task.FromResult(OperationResult.Ok($"{hiveName}\\{subkeyPath}\\{valueName} already absent"));
                }
                key.DeleteValue(valueName, throwOnMissingValue: false);
                context.Log($"deleted value {hiveName}\\{subkeyPath}\\{valueName}");
                return Task.FromResult(OperationResult.Ok($"{hiveName}\\{subkeyPath}\\{valueName} deleted"));
            }

            if (string.IsNullOrEmpty(subkeyPath))
                return Task.FromResult(OperationResult.Fail($"refusing to delete hive root '{hiveName}'"));

            root.DeleteSubKeyTree(subkeyPath, throwOnMissingSubKey: false);
            context.Log($"deleted key tree {hiveName}\\{subkeyPath}");
            return Task.FromResult(OperationResult.Ok($"{hiveName}\\{subkeyPath} deleted"));
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
