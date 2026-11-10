namespace WinEvo.Actions.Abstractions;

/// <summary>
/// One atomic thing the agent can do to the system: write a registry value,
/// kill a process, run a tool. Concrete subclasses carry the typed data for
/// their operation (e.g. <c>RegistrySetOperation</c> carries the key, the
/// value name and the data) and implement <see cref="ExecuteAsync"/> against
/// it. Actions build operations in code and run them through
/// <see cref="ActionExecution"/>.
///
/// <para>
/// TODO: progress streaming and undo hooks as the action model evolves.
/// </para>
/// </summary>
public abstract class ActionOperation
{
    /// <summary>Operation name reported in step results and logs, e.g. "registry-set".</summary>
    public abstract string Id { get; }

    public abstract Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken);
}
