using System.Text.Json;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Default <see cref="IOperationParser"/>: maps a manifest's <c>operation</c>
/// id string to the concrete <see cref="ActionOperation"/> subclass. Each
/// operation registers its own <c>static FromJson(JsonElement)</c> factory
/// here, so schema-binding knowledge stays colocated with the operation it
/// describes. Unknown ids fall back to <see cref="UnknownActionOperation"/>.
/// </summary>
public sealed class OperationParser : IOperationParser
{
    private static readonly Dictionary<string, Func<JsonElement, ActionOperation>> s_factories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["registry-set"]     = RegistrySetOperation.FromJson,
        ["registry-delete"]  = RegistryDeleteOperation.FromJson,
        ["process-kill"]     = ProcessKillOperation.FromJson,
        ["external-process"] = ExternalProcessOperation.FromJson,
        ["builtin-tool"]     = BuiltinToolOperation.FromJson,
        ["powershell"]       = PowerShellOperation.FromJson,
        ["command"]          = CommandOperation.FromJson,
        ["delay"]            = DelayOperation.FromJson,
        // TODO: registry-read, service-stop, service-start, service-restart,
        //       file-delete, file-copy, file-move, sysinternals-tool,
        //       system-restore-point.
    };

    public IReadOnlyCollection<string> SupportedIds => s_factories.Keys;

    public ActionOperation Parse(string operationId, JsonElement properties)
        => s_factories.TryGetValue(operationId, out var factory)
            ? factory(properties)
            : new UnknownActionOperation(operationId, properties.Clone());
}
