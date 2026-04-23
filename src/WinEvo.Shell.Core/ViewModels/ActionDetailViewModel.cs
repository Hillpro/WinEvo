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

    public ActionDetailViewModel(
        ActionItemViewModel item,
        string? language,
        AgentLauncher agentLauncher,
        DispatcherQueue dispatcher)
    {
        _agentLauncher = agentLauncher;
        _dispatcher = dispatcher;
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
            var client = await ResolveClientAsync(ct).ConfigureAwait(false);
            if (client is null)
                return;

            await RunOnUiAsync(() => Status = "Running…").ConfigureAwait(false);

            var manifestJson = JsonNode.Parse(Item.Manifest.RawJson)
                ?? throw new InvalidOperationException("manifest RawJson is not valid JSON");
            var paramDict = Parameters.ToDictionary(p => p.Id, p => p.ToJsonValue());

            var response = await client.ExecuteAsync(manifestJson, paramDict, ct).ConfigureAwait(false);

            await RunOnUiAsync(() =>
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
            await RunOnUiAsync(() =>
            {
                Status = $"Error: {ex.Message}";
                ResultDetail = ex.ToString();
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsRunning = false).ConfigureAwait(false);
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
            await RunOnUiAsync(() => Status = "Waiting for elevation…").ConfigureAwait(false);
            try
            {
                return await _agentLauncher.EnsureElevatedAsync(ct).ConfigureAwait(false);
            }
            catch (ElevationCancelledException)
            {
                await RunOnUiAsync(() => Status = "Elevation was declined. Action not executed.").ConfigureAwait(false);
                return null;
            }
        }

        var client = _agentLauncher.Client;
        if (client is null || !client.IsConnected)
        {
            await RunOnUiAsync(() => Status = "Agent not connected.").ConfigureAwait(false);
            return null;
        }
        return client;
    }

    private bool CanExecute() => !IsRunning;

    private Task RunOnUiAsync(Action action)
    {
        if (_dispatcher.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();
        var enqueued = _dispatcher.TryEnqueue(() =>
        {
            try { action(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        if (!enqueued)
            tcs.SetException(new InvalidOperationException("failed to enqueue UI update"));
        return tcs.Task;
    }

}
