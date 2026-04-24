using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Maps operation ids (as they appear in manifests) to <see cref="IActionOperation"/>
/// implementations. Eight operations wired; others are TODO.
/// </summary>
public sealed class OperationCatalog
{
    private readonly Dictionary<string, IActionOperation> _operations;

    public OperationCatalog(IEnumerable<IActionOperation> operations)
    {
        _operations = operations.ToDictionary(op => op.Id, StringComparer.OrdinalIgnoreCase);
    }

    public static OperationCatalog Default() => new(
    [
        new RegistrySetOperation(),
        new RegistryDeleteOperation(),
        new ProcessKillOperation(),
        new ExternalProcessOperation(),
        new BuiltinToolOperation(),
        new PowerShellOperation(),
        new CommandOperation(),
        new DelayOperation(),
        // TODO: registry-read, service-stop, service-start, service-restart,
        //       file-delete, file-copy, file-move, sysinternals-tool,
        //       system-restore-point.
    ]);

    public IReadOnlyCollection<string> SupportedIds => _operations.Keys;

    public bool TryGet(string id, out IActionOperation operation)
        => _operations.TryGetValue(id, out operation!);
}
