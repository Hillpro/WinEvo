using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using WinEvo.ActionModel;
using WinEvo.Shell.Core.Services;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>
/// Right-pane VM: shows the selected action, hosts parameter inputs, and exposes
/// the <see cref="ExecuteCommand"/>. Ensures the agent is elevated when the
/// action declares <c>elevation: required</c>. Displays step-result summary
/// inline. TODO: streaming progress events from the agent.
/// </summary>
public sealed partial class ActionDetailViewModel : ObservableObject
{
    private readonly AgentLauncher _agentLauncher;
    private readonly DispatcherQueue _dispatcher;
    private readonly IConfirmationService _confirmation;
    private readonly StringBundle _strings;

    public ActionDetailViewModel(
        ActionItemViewModel item,
        string? language,
        AgentLauncher agentLauncher,
        DispatcherQueue dispatcher,
        IConfirmationService confirmation,
        StringBundle strings)
    {
        _agentLauncher = agentLauncher;
        _dispatcher = dispatcher;
        _confirmation = confirmation;
        _strings = strings;
        Item = item;
        Language = language;

        foreach (var p in item.Manifest.Parameters)
            Parameters.Add(new ParameterInputViewModel(p, language));
    }

    public ActionItemViewModel Item { get; }
    public string? Language { get; }

    public ObservableCollection<ParameterInputViewModel> Parameters { get; } = new();

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial string? Status { get; set; }

    [ObservableProperty]
    public partial string? ResultDetail { get; set; }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ExecuteAsync(CancellationToken ct)
    {
        var missingRequired = Parameters
            .Where(p => p.Required && string.IsNullOrWhiteSpace(p.Value))
            .ToList();
        if (missingRequired.Count > 0)
        {
            Status = $"Missing required parameter(s): {string.Join(", ", missingRequired.Select(p => p.DisplayName))}";
            return;
        }

        IsRunning = true;
        Status = null;
        ResultDetail = null;

        try
        {
            var warnings = WarningAggregator.Aggregate(Item.Manifest.Warnings, _strings, Language);
            if (warnings.Count > 0)
            {
                var request = new ConfirmationRequest(Item.DisplayName, warnings);
                var accepted = await _confirmation.RequestAsync(request, ct).ConfigureAwait(false);
                if (!accepted)
                {
                    await _dispatcher.RunOnUiAsync(() => Status = "Cancelled by user.").ConfigureAwait(false);
                    return;
                }
            }

            var client = await ResolveClientAsync(ct).ConfigureAwait(false);
            if (client is null)
                return;

            await _dispatcher.RunOnUiAsync(() => Status = "Running…").ConfigureAwait(false);

            var manifestJson = JsonNode.Parse(Item.Manifest.RawJson)
                ?? throw new InvalidOperationException("manifest RawJson is not valid JSON");
            var paramDict = Parameters.ToDictionary(p => p.Id, p => p.ToJsonValue());

            var response = await client.ExecuteAsync(manifestJson, paramDict, ct).ConfigureAwait(false);

            await _dispatcher.RunOnUiAsync(() =>
            {
                Status = response.Success
                    ? $"Success — {response.Message}"
                    : $"Failed — {response.Message}";

                var detail = new System.Text.StringBuilder();
                foreach (var step in response.StepResults)
                {
                    var ok = step.Success ? "OK" : "FAIL";
                    detail.AppendLine($"[{ok}] {step.Operation}"
                        + (step.StepId is null ? "" : $" ({step.StepId})")
                        + (step.Message is null ? "" : $" — {step.Message}")
                        + (step.Error is null ? "" : $" — {step.Error}"));
                }
                foreach (var line in response.Log)
                    detail.AppendLine(line);
                ResultDetail = detail.Length == 0 ? null : detail.ToString().TrimEnd();
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _dispatcher.RunOnUiAsync(() =>
            {
                Status = $"Error: {ex.Message}";
                ResultDetail = ex.ToString();
            }).ConfigureAwait(false);
        }
        finally
        {
            await _dispatcher.RunOnUiAsync(() => IsRunning = false).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Returns the agent client to use for this execution, prompting for UAC
    /// elevation when the selected action requires it. Updates <see cref="Status"/>
    /// and returns <see langword="null"/> when the user cancels UAC or no agent
    /// is reachable.
    /// </summary>
    private async Task<IAgentClient?> ResolveClientAsync(CancellationToken ct)
    {
        var requiresElevation = Item.Manifest.Requirements.Elevation == ElevationRequirement.Required;

        if (requiresElevation && !_agentLauncher.IsElevated)
        {
            await _dispatcher.RunOnUiAsync(() => Status = "Waiting for elevation…").ConfigureAwait(false);
            try
            {
                return await _agentLauncher.EnsureElevatedAsync(ct).ConfigureAwait(false);
            }
            catch (ElevationCancelledException)
            {
                await _dispatcher.RunOnUiAsync(() => Status = "Elevation was declined. Action not executed.").ConfigureAwait(false);
                return null;
            }
        }

        var client = _agentLauncher.Client;
        if (client is null || !client.IsConnected)
        {
            await _dispatcher.RunOnUiAsync(() => Status = "Agent not connected.").ConfigureAwait(false);
            return null;
        }
        return client;
    }

    private bool CanExecute() => !IsRunning;

}
