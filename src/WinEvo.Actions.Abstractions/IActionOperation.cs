using WinEvo.ActionModel;

namespace WinEvo.Actions.Abstractions;

/// <summary>
/// Contract for an operation implementation (e.g. registry-set, external-process,
/// command). TODO: finalize cancellation, progress streaming, and undo hooks
/// as the action model evolves.
/// </summary>
public interface IActionOperation
{
    /// <summary>Operation id as it appears in action manifests, e.g. "registry-set".</summary>
    string Id { get; }

    /// <summary>Whether this operation typically requires elevation. Informational; the runtime decides actual routing.</summary>
    bool RequiresElevation { get; }

    /// <summary>Execute the operation. Implementations should render template expressions in step properties via <see cref="Templating"/>.</summary>
    Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken);
}
