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
/// When <c>value</c> is present (even as an empty string — the default-value
/// slot), only that value is removed. When <c>value</c> is absent, the whole
/// subkey tree under <c>key</c> is removed. Idempotent: deleting something
/// that is already absent succeeds.
/// </summary>
public sealed class RegistryDeleteOperation : IActionOperation
{
    public string Id => "registry-delete";

    public Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        try
        {
            var keyPath = context.RenderProperty("key");
            if (string.IsNullOrWhiteSpace(keyPath))
                return Task.FromResult(OperationResult.Fail("missing 'key' property"));

            var (hiveName, subkeyPath) = RegistryPath.SplitHive(keyPath);
            var root = RegistryPath.ResolveHive(hiveName);
            if (root is null)
                return Task.FromResult(OperationResult.Fail($"unknown hive '{hiveName}' in key path '{keyPath}'"));

            // Value property presence (not emptiness) selects between value- and
            // key-delete. An empty string IS the "default value" of a key.
            var deletingValue = context.Step.Properties.TryGetProperty("value", out var valueProp)
                && valueProp.ValueKind == System.Text.Json.JsonValueKind.String;

            if (deletingValue)
            {
                var valueName = context.RenderProperty("value");
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
