using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using WinEvo.Ipc;
using WinEvo.Shell.Core.Services;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>
/// Top-level VM for the Shell's main window. Owns the action catalog and the
/// current selection; delegates agent lifetime/elevation to <see cref="AgentLauncher"/>
/// and reflects its state via <see cref="AgentStatus"/>.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ActionCatalog _catalog;
    private readonly AgentLauncher _agentLauncher;
    private readonly string? _language;
    private readonly DispatcherQueue _dispatcher;

    public MainViewModel(ActionCatalog catalog, AgentLauncher agentLauncher, string? language, DispatcherQueue dispatcher)
    {
        _catalog = catalog;
        _agentLauncher = agentLauncher;
        _language = language;
        _dispatcher = dispatcher;
        _agentLauncher.StateChanged += OnAgentStateChanged;
    }

    public ObservableCollection<ActionItemViewModel> Actions { get; } = new();

    [ObservableProperty]
    public partial ActionItemViewModel? SelectedAction { get; set; }

    [ObservableProperty]
    public partial ActionDetailViewModel? Detail { get; set; }

    [ObservableProperty]
    public partial string AgentStatus { get; set; } = "Not connected";

    partial void OnSelectedActionChanged(ActionItemViewModel? value)
    {
        Detail = value is null
            ? null
            : new ActionDetailViewModel(value, _language, _agentLauncher, _dispatcher);
    }

    [RelayCommand]
    private async Task InitialiseAsync(CancellationToken ct)
    {
        await _catalog.LoadAsync(ct).ConfigureAwait(false);

        await RunOnUiAsync(() =>
        {
            Actions.Clear();
            foreach (var manifest in _catalog.Manifests.OrderBy(m => m.Category).ThenBy(m => m.Name))
                Actions.Add(new ActionItemViewModel(manifest, _language));
        }).ConfigureAwait(false);

        try
        {
            await RunOnUiAsync(() => AgentStatus = "Starting agent…").ConfigureAwait(false);
            await _agentLauncher.StartAsync(elevated: false, ct).ConfigureAwait(false);
            // OnAgentStateChanged fires from StartAsync and updates AgentStatus.
        }
        catch (Exception ex)
        {
            await RunOnUiAsync(() => AgentStatus = $"Agent error: {ex.Message}").ConfigureAwait(false);
        }
    }

    private void OnAgentStateChanged()
    {
        _dispatcher.TryEnqueue(() => AgentStatus = FormatStatus(_agentLauncher.LastHandshake, _agentLauncher.IsElevated));
    }

    private static string FormatStatus(HandshakeResponse? handshake, bool elevated)
    {
        if (handshake is null)
            return "Not connected";
        var badge = elevated ? " (elevated)" : "";
        return $"Connected — agent {handshake.AgentVersion}, {handshake.SupportedOperations.Count} operations{badge}";
    }

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
