namespace WinEvo.Actions.Abstractions;

/// <summary>
/// The set of actions available in this process. The Shell lists
/// <see cref="Actions"/> to build its catalog; the agent resolves an incoming
/// action id with <see cref="Find"/>.
/// </summary>
public interface IActionRegistry
{
    IReadOnlyList<ActionDefinition> Actions { get; }

    /// <summary>The action with this id, or <see langword="null"/> when none is registered.</summary>
    ActionDefinition? Find(string actionId);
}
