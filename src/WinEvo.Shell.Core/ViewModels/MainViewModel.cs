using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using WinEvo.ActionModel;
using WinEvo.Shell.Core.Services;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>
/// Top-level VM for the Shell's main window. Owns the action catalog, the
/// current selection, and the detail VM. Agent connection is lazy and
/// reported via <see cref="AgentStatus"/>.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ActionCatalog _catalog;
    private readonly AgentLauncher _agentLauncher;
    private readonly string? _language;
    private readonly DispatcherQueue _dispatcher;
    private IAgentClient? _agentClient;

    public MainViewModel(ActionCatalog catalog, AgentLauncher agentLauncher, string? language, DispatcherQueue dispatcher)
    {
        _catalog = catalog;
        _agentLauncher = agentLauncher;
        _language = language;
        _dispatcher = dispatcher;
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
            : new ActionDetailViewModel(value, _language, () => _agentClient, _dispatcher);
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
            _agentClient = await _agentLauncher.StartAsync(ct).ConfigureAwait(false);
            var handshake = await _agentClient.HandshakeAsync(ct).ConfigureAwait(false);
            await RunOnUiAsync(() =>
                AgentStatus = $"Connected — agent {handshake.AgentVersion}, {handshake.SupportedOperations.Count} operations"
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RunOnUiAsync(() => AgentStatus = $"Agent error: {ex.Message}").ConfigureAwait(false);
        }
    }

    /// <summary>Marshals <paramref name="action"/> to the UI dispatcher and completes when it has run.</summary>
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
