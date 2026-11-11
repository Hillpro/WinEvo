using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using WinEvo.Actions.Abstractions;
using WinEvo.Ipc;
using WinEvo.Shell.Core.Services;
using WinEvo.Shell.Core.ViewModels.Interactions;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>
/// Right-pane VM: shows the selected action, hosts parameter inputs, exposes
/// the <see cref="ExecuteCommand"/>, and surfaces an <see cref="Interaction"/>
/// controller that owns mode-specific UI glue (button vs toggle today; future
/// modes plug in here). Ensures the agent is elevated when the action declares
/// <c>elevation: required</c>. Displays step-result summary inline.
/// TODO: streaming progress events from the agent.
/// </summary>
public sealed partial class ActionDetailViewModel : ObservableObject, IDisposable
{
    private readonly AgentLauncher _agentLauncher;
    private readonly ExecutionTracker _executions;
    private readonly DispatcherQueue _dispatcher;
    private readonly IConfirmationService _confirmation;
    private readonly CancellationTokenSource _liveStateRead = new();
    private bool _disposed;

    public ActionDetailViewModel(
        ActionItemViewModel item,
        CultureInfo culture,
        AgentLauncher agentLauncher,
        ExecutionTracker executions,
        DispatcherQueue dispatcher,
        IConfirmationService confirmation,
        IParameterInputFactory parameterFactory)
    {
        _agentLauncher = agentLauncher;
        _executions = executions;
        _dispatcher = dispatcher;
        _confirmation = confirmation;
        Item = item;
        Culture = culture;

        foreach (var p in item.Descriptor.Parameters)
            Parameters.Add(parameterFactory.Create(p, culture));

        Interaction = item.Descriptor.Interaction switch
        {
            InteractionMode.Toggle => new ToggleInteractionController(this),
            _ => new ButtonInteractionController(this),
        };

        var runningResponse = executions.FindRunning(item.Id);
        if (runningResponse is not null)
            _ = FollowRunningExecutionAsync(runningResponse);

        _ = ShowLiveStateAsync(_liveStateRead.Token);
    }

    public ActionItemViewModel Item { get; }
    public CultureInfo Culture { get; }

    public ObservableCollection<ParameterInputViewModel> Parameters { get; } = new();

    public bool HasParameters => Parameters.Count > 0;

    /// <summary>Per-mode UI glue (button, toggle, …). XAML selects a template by this object's runtime type.</summary>
    public InteractionController Interaction { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteCommand))]
    public partial bool IsRunning { get; set; }

    /// <summary>True while the agent is being asked what the system currently looks like.</summary>
    [ObservableProperty]
    public partial bool IsReadingLiveState { get; set; }

    [ObservableProperty]
    public partial string? Status { get; set; }

    [ObservableProperty]
    public partial string? ResultDetail { get; set; }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ExecuteAsync(CancellationToken ct)
    {
        var missingRequired = Parameters
            .Where(p => p.Required && !p.HasValue)
            .ToList();
        if (missingRequired.Count > 0)
        {
            Status = Text("Status_MissingParameters", string.Join(", ", missingRequired.Select(p => p.DisplayName)));
            return;
        }

        IsRunning = true;
        Status = null;
        ResultDetail = null;

        try
        {
            var warnings = Item.Descriptor.Warnings
                .Select(warning => new ResolvedWarning(warning.Severity, warning.Message.Resolve(Culture)))
                .ToList();
            if (warnings.Count > 0)
            {
                var request = new ConfirmationRequest(Item.DisplayName, warnings);
                var accepted = await _confirmation.RequestAsync(request, ct).ConfigureAwait(false);
                if (!accepted)
                {
                    await _dispatcher.RunOnUiAsync(() => Status = Text("Status_CancelledByUser")).ConfigureAwait(false);
                    return;
                }
            }

            var client = await ResolveClientAsync(ct).ConfigureAwait(false);
            if (client is null)
                return;

            await _dispatcher.RunOnUiAsync(() => Status = Text("Status_Running")).ConfigureAwait(false);

            var paramDict = Parameters.ToDictionary(p => p.Id, p => p.ToJsonValue());

            var response = await _executions.RunAsync(client, Item.Id, paramDict, ct).ConfigureAwait(false);

            await _dispatcher.RunOnUiAsync(() => ShowOutcome(response)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _dispatcher.RunOnUiAsync(() => ShowError(ex)).ConfigureAwait(false);
        }
        finally
        {
            await _dispatcher.RunOnUiAsync(() => IsRunning = false).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Shows as running an execution of this action that was started before
    /// the user selected it again, then shows how that execution ended, so
    /// the action never looks idle while the agent is still running it.
    /// </summary>
    private async Task FollowRunningExecutionAsync(Task<ExecutionResponse> runningResponse)
    {
        await _dispatcher.RunOnUiAsync(() =>
        {
            IsRunning = true;
            Status = Text("Status_Running");
        }).ConfigureAwait(false);

        try
        {
            var response = await runningResponse.ConfigureAwait(false);
            await _dispatcher.RunOnUiAsync(() => ShowOutcome(response)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _dispatcher.RunOnUiAsync(() => ShowError(ex)).ConfigureAwait(false);
        }
        finally
        {
            await _dispatcher.RunOnUiAsync(() => IsRunning = false).ConfigureAwait(false);
        }
    }

    private void ShowOutcome(ExecutionResponse response)
    {
        Status = ExecutionOutcomeText.For(response, Culture);

        // The agent's own account of the run. It is diagnostic output and
        // stays in English, like the agent's log.
        var detail = new System.Text.StringBuilder();
        if (response.Message is not null)
            detail.AppendLine(response.Message);
        foreach (var step in response.StepResults)
        {
            var outcome = step.Success ? "OK" : step.FailureTolerated ? "WARN" : "FAIL";
            detail.AppendLine($"[{outcome}] {step.Operation}"
                + (step.Message is null ? "" : $" — {step.Message}")
                + (step.Error is null ? "" : $" — {step.Error}"));
        }
        foreach (var line in response.Log)
            detail.AppendLine(line);
        ResultDetail = detail.Length == 0 ? null : detail.ToString().TrimEnd();
    }

    private void ShowError(Exception ex)
    {
        Status = Text("Status_Error", ex.Message);
        ResultDetail = ex.ToString();
    }

    /// <summary>
    /// Returns the agent client to use for this execution, prompting for UAC
    /// elevation when the selected action requires it. Updates <see cref="Status"/>
    /// and returns <see langword="null"/> when the user cancels UAC or no agent
    /// is reachable.
    /// </summary>
    private async Task<IAgentClient?> ResolveClientAsync(CancellationToken ct)
    {
        var requiresElevation = Item.RequiresElevation;

        if (requiresElevation && !_agentLauncher.IsElevated)
        {
            await _dispatcher.RunOnUiAsync(() => Status = Text("Status_WaitingForElevation")).ConfigureAwait(false);
            try
            {
                return await _agentLauncher.EnsureElevatedAsync(ct).ConfigureAwait(false);
            }
            catch (ElevationCancelledException ex)
            {
                // Windows reports a declined prompt and a silently blocked
                // launch with the same error, so say which one we believe it
                // was rather than blaming the user for a prompt they may never
                // have been shown.
                var message = Text(ex.Reason == ElevationFailureReason.DownloadMarkerPresent
                    ? "Status_BlockedDownload"
                    : "Status_ElevationDeclined");

                await _dispatcher.RunOnUiAsync(() => Status = message).ConfigureAwait(false);
                return null;
            }
        }

        var client = _agentLauncher.Client;
        if (client is null || !client.IsConnected)
        {
            await _dispatcher.RunOnUiAsync(() => Status = Text("Status_AgentNotConnected")).ConfigureAwait(false);
            return null;
        }
        return client;
    }

    private bool CanExecute() => !IsRunning;

    private string Text(string key, params object?[] arguments) => ViewModelText.Format(key, Culture, arguments);

    /// <summary>
    /// Replaces each parameter's declared default with the value currently in
    /// effect on the system, as reported by the agent. When the agent is not
    /// reachable or the read fails, the defaults stay: they are the fallback,
    /// not the expected path.
    /// </summary>
    private async Task ShowLiveStateAsync(CancellationToken ct)
    {
        var client = _agentLauncher.Client;
        if (client is null || !client.IsConnected)
            return;

        // The agent answers one request at a time, so the read waits for any
        // running execution to end: hours, for a free-space wipe. Say so,
        // rather than leave a disabled switch and a spinner unexplained.
        var waitingForAnotherAction = _executions.RunningActionIds.Any(actionId => actionId != Item.Id)
            ? Text("Status_WaitingForAnotherAction")
            : null;

        await _dispatcher.RunOnUiAsync(() =>
        {
            IsReadingLiveState = true;
            Status ??= waitingForAnotherAction;
        }).ConfigureAwait(false);
        try
        {
            var liveValues = await client.ReadStateAsync(Item.Id, ct).ConfigureAwait(false);
            await _dispatcher.RunOnUiAsync(() =>
            {
                foreach (var parameter in Parameters)
                {
                    if (!liveValues.TryGetValue(parameter.Id, out var liveValue) || liveValue is null)
                        continue;

                    // One input refusing its value (a drive that is no longer
                    // mounted) must not leave the others on their defaults.
                    try
                    {
                        parameter.ShowLiveValue(liveValue);
                    }
                    catch (Exception ex)
                    {
                        ShellLog.WriteException($"could not show the live value of '{Item.Id}' parameter '{parameter.Id}'", ex);
                    }
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The user moved on to another action before the read was sent.
        }
        catch (Exception ex)
        {
            ShellLog.WriteException($"could not read the live state of '{Item.Id}'", ex);
        }
        finally
        {
            await _dispatcher.RunOnUiAsync(() =>
            {
                IsReadingLiveState = false;
                if (waitingForAnotherAction is not null && Status == waitingForAnotherAction)
                    Status = null;
            }).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Abandons a live-state read that has not been sent yet and releases the
    /// interaction controller. Called by <see cref="MainViewModel"/> when the
    /// user picks a different action, and at window close.
    /// </summary>
    public void Dispose()
    {
        // Reached twice when the user picks another action while the window
        // is closing: once from the close handler, once from the selection.
        if (_disposed)
            return;
        _disposed = true;

        _liveStateRead.Cancel();
        _liveStateRead.Dispose();
        (Interaction as IDisposable)?.Dispose();
    }
}
