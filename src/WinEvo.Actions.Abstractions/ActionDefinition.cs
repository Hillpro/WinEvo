using System.Collections.ObjectModel;

namespace WinEvo.Actions.Abstractions;

/// <summary>
/// An action the agent can run, written in code. The <see cref="Descriptor"/>
/// is the data the Shell renders; <see cref="ExecuteAsync"/> and
/// <see cref="ReadStateAsync"/> run only inside the agent.
/// </summary>
public abstract class ActionDefinition
{
    public abstract ActionDescriptor Descriptor { get; }

    /// <summary>
    /// Performs the action. Every operation goes through <paramref name="execution"/>
    /// so it is recorded as a step of the execution.
    /// </summary>
    public abstract Task ExecuteAsync(ActionExecution execution, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the live system value behind each parameter that mirrors system
    /// state, keyed by parameter id. A parameter left out keeps its declared
    /// default in the UI. Actions with no such parameter keep this default.
    /// </summary>
    public virtual Task<IReadOnlyDictionary<string, object?>> ReadStateAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyDictionary<string, object?>>(ReadOnlyDictionary<string, object?>.Empty);
}
