using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private readonly IConfirmationService _confirmation;
    private readonly StringBundle _strings;
    private readonly IParameterInputFactory _parameterFactory;

    public MainViewModel(
        ActionCatalog catalog,
        AgentLauncher agentLauncher,
        string? language,
        DispatcherQueue dispatcher,
        IConfirmationService confirmation,
        StringBundle strings,
        IParameterInputFactory parameterFactory)
    {
        _catalog = catalog;
        _agentLauncher = agentLauncher;
        _language = language;
        _dispatcher = dispatcher;
        _confirmation = confirmation;
        _strings = strings;
        _parameterFactory = parameterFactory;
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
            : new ActionDetailViewModel(value, _language, _agentLauncher, _dispatcher, _confirmation, _strings, _parameterFactory);
    }

    partial void OnDetailChanged(ActionDetailViewModel? oldValue, ActionDetailViewModel? newValue)
    {
        // Subscribe to the active Detail's IsRunning so the footer reflects
        // execution state without any extra binding.
        if (oldValue is not null) oldValue.PropertyChanged -= OnDetailPropertyChanged;
        if (newValue is not null) newValue.PropertyChanged += OnDetailPropertyChanged;

        oldValue?.Dispose();
        RefreshAgentStatus();
    }

    private void OnDetailPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ActionDetailViewModel.IsRunning))
            _dispatcher.TryEnqueue(RefreshAgentStatus);
    }

    [RelayCommand]
    private async Task InitialiseAsync(CancellationToken ct)
    {
        await _catalog.LoadAsync(ct).ConfigureAwait(false);

        await _dispatcher.RunOnUiAsync(() =>
        {
            Actions.Clear();
            foreach (var manifest in _catalog.Manifests.OrderBy(m => m.Category).ThenBy(m => m.Name))
                Actions.Add(new ActionItemViewModel(manifest, _language));
        }).ConfigureAwait(false);

        try
        {
            await _dispatcher.RunOnUiAsync(() => AgentStatus = "Starting agent…").ConfigureAwait(false);
            await _agentLauncher.StartAsync(elevated: false, ct).ConfigureAwait(false);
            // OnAgentStateChanged fires from StartAsync and updates AgentStatus.
        }
        catch (Exception ex)
        {
            await _dispatcher.RunOnUiAsync(() => AgentStatus = $"Agent error: {ex.Message}").ConfigureAwait(false);
        }
    }

    private void OnAgentStateChanged()
    {
        _dispatcher.TryEnqueue(RefreshAgentStatus);
    }

    private void RefreshAgentStatus()
    {
        // The agent only runs one execution at a time today, so observing the
        // current Detail is sufficient. TODO: aggregate IsRunning across every
        // ActionDetailViewModel once multi-execution is supported (or, more
        // immediately, once switching selection mid-execution should still
        // count the prior VM — right now it doesn't because Detail is swapped).
        var runningCount = Detail?.IsRunning == true ? 1 : 0;
        AgentStatus = FormatStatus(_agentLauncher.LastHandshake, _agentLauncher.IsElevated, runningCount);
    }

    private static string FormatStatus(HandshakeResponse? handshake, bool elevated, int runningCount)
    {
        if (handshake is null)
            return "Not connected";
        var badge = elevated ? " (elevated)" : "";
        var runningLabel = runningCount == 1 ? "1 running" : $"{runningCount} running";
        return $"Connected — agent {handshake.AgentVersion}{badge} · {runningLabel}";
    }

}
