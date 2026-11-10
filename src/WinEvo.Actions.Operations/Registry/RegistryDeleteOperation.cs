using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Deletes a registry value or a registry key (subtree):
/// <code>
/// // Delete a single value:
/// new RegistryDeleteOperation { Key = @"HKCU\Software\Foo", ValueName = "MyValue" }
///
/// // Delete a key and everything under it:
/// new RegistryDeleteOperation { Key = @"HKCU\Software\Foo" }
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

    public override Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Key))
                return Task.FromResult(OperationResult.Fail("empty key path"));

            var (hiveName, subkeyPath) = RegistryPath.SplitHive(Key);
            var root = RegistryPath.ResolveHive(hiveName);
            if (root is null)
                return Task.FromResult(OperationResult.Fail($"unknown hive '{hiveName}' in key path '{Key}'"));

            if (ValueName is not null)
            {
                if (string.IsNullOrEmpty(subkeyPath))
                    return Task.FromResult(OperationResult.Fail($"cannot delete values from hive root '{hiveName}'"));

                using var key = root.OpenSubKey(subkeyPath, writable: true);
                if (key is null)
                {
                    context.Log($"key {hiveName}\\{subkeyPath} not present; nothing to delete");
                    return Task.FromResult(OperationResult.Ok($"{hiveName}\\{subkeyPath}\\{ValueName} already absent"));
                }
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                context.Log($"deleted value {hiveName}\\{subkeyPath}\\{ValueName}");
                return Task.FromResult(OperationResult.Ok($"{hiveName}\\{subkeyPath}\\{ValueName} deleted"));
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
