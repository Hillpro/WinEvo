using System.Text.Json;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Forward-compat fallback for manifests that name an operation id this agent
/// build doesn't know how to execute. Parsing succeeds (so the rest of the
/// manifest loads); execution reports "not implemented" without modifying the
/// system. Keeps old agents gracefully handling newer manifests.
/// </summary>
public sealed class UnknownActionOperation : ActionOperation
{
    public override string Id { get; }

    /// <summary>Preserved raw properties in case a diagnostic wants to surface them.</summary>
    public JsonElement RawProperties { get; }

    public UnknownActionOperation(string id, JsonElement rawProperties)
    {
        Id = id;
        RawProperties = rawProperties;
    }

    public override Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
        => Task.FromResult(OperationResult.Fail($"operation '{Id}' is not implemented by this agent"));
}
