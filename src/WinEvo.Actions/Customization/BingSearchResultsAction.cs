using WinEvo.ActionModel;
using WinEvo.Actions.Abstractions;
using WinEvo.Actions.Operations;

namespace WinEvo.Actions;

/// <summary>
/// Shows or hides Bing web results in the Start and Search experience of the
/// current user.
/// </summary>
public sealed class BingSearchResultsAction : ToggleActionDefinition
{
    private const string SearchKey = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Search";
    private const string BingSearchEnabledValue = "BingSearchEnabled";
    private const string CortanaConsentValue = "CortanaConsent";

    public BingSearchResultsAction()
        : base(
            new ActionDescriptor
            {
                Id = "customization.bing-search-results",
                Category = "customization",
                Name = ActionText.For("BingSearchResults_Name"),
                Description = ActionText.For("BingSearchResults_Description"),
                Tags = ["privacy", "search", "start-menu"],
                Icon = "search-off",
                Author = "Hillpro",
            },
            switchLabel: ActionText.For("BingSearchResults_SwitchLabel"),
            switchDescription: ActionText.For("BingSearchResults_SwitchDescription"),
            // Windows shows Bing results until the value is written, so an
            // absent value means "on".
            isOnWhenUnknown: true)
    {
    }

    protected override Task<bool?> ReadIsOnAsync(CancellationToken cancellationToken)
    {
        var bingSearchEnabled = RegistryValueReader.Read(SearchKey, BingSearchEnabledValue);
        return Task.FromResult<bool?>(bingSearchEnabled is null ? null : bingSearchEnabled is 1);
    }

    protected override async Task ApplyAsync(bool isOn, ActionExecution execution, CancellationToken cancellationToken)
    {
        var enabled = isOn ? 1 : 0;
        await execution.RunAsync(RegistrySetOperation.Dword(SearchKey, BingSearchEnabledValue, enabled), cancellationToken).ConfigureAwait(false);
        await execution.RunAsync(RegistrySetOperation.Dword(SearchKey, CortanaConsentValue, enabled), cancellationToken).ConfigureAwait(false);

        // The search host picks the new values up on its next start. It may
        // not be running, and it restarts on demand, so a failed kill is not
        // a failed action.
        await execution.TryRunAsync(new ProcessKillOperation { Name = "SearchUI" }, cancellationToken).ConfigureAwait(false);
    }
}
