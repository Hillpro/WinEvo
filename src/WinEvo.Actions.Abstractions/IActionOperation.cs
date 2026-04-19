namespace WinEvo.Actions.Abstractions;

/// <summary>
/// Contract for an operation implementation (e.g. registry-set, external-process,
/// command). TODO: flesh out the execution contract (parameters, result,
/// cancellation, progress) alongside the ActionModel step types.
/// </summary>
public interface IActionOperation
{
    /// <summary>Operation id as it appears in action manifests, e.g. "registry-set".</summary>
    string Id { get; }
}
