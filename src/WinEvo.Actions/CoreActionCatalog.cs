using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions;

/// <summary>
/// The actions that ship with WinEvo. Adding an action = one new
/// <see cref="ActionDefinition"/> subclass + one line in the list below.
/// </summary>
public sealed class CoreActionCatalog : IActionCatalog
{
    private readonly Dictionary<string, ActionDefinition> _actionsById;

    public CoreActionCatalog()
    {
        Actions =
        [
            new BingSearchResultsAction(),
            new WipeFreeSpaceAction(),
        ];
        _actionsById = Actions.ToDictionary(action => action.Descriptor.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<ActionDefinition> Actions { get; }

    public ActionDefinition? Find(string actionId) => _actionsById.GetValueOrDefault(actionId);
}
