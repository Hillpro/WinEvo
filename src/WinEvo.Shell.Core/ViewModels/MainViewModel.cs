using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using WinEvo.Actions.Abstractions;
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
    private readonly AgentLauncher _agentLauncher;
    private readonly CultureInfo _culture;
    private readonly DispatcherQueue _dispatcher;
    private readonly IConfirmationService _confirmation;
    private readonly IParameterInputFactory _parameterFactory;

    /// <param name="catalog">
    /// Source of the action list. Only descriptors are read here; actions are
    /// executed by the agent, never in this process.
    /// </param>
    /// <param name="culture">Language every action text is displayed in.</param>
    public MainViewModel(
        IActionCatalog catalog,
        AgentLauncher agentLauncher,
        CultureInfo culture,
        DispatcherQueue dispatcher,
        IConfirmationService confirmation,
        IParameterInputFactory parameterFactory)
    {
        _agentLauncher = agentLauncher;
        _culture = culture;
        _dispatcher = dispatcher;
        _confirmation = confirmation;
        _parameterFactory = parameterFactory;
        _agentLauncher.StateChanged += OnAgentStateChanged;
        AgentStatus = ViewModelText.Get("Agent_NotConnected", culture);

        var items = catalog.Actions
            .Select(action => new ActionItemViewModel(action.Descriptor, culture))
            .OrderBy(item => item.Category, StringComparer.Ordinal)
            .ThenBy(item => item.DisplayName, StringComparer.Create(culture, ignoreCase: true));
        Actions = new ObservableCollection<ActionItemViewModel>(items);
    }

    public ObservableCollection<ActionItemViewModel> Actions { get; }

    [ObservableProperty]
    public partial ActionItemViewModel? SelectedAction { get; set; }

    [ObservableProperty]
    public partial ActionDetailViewModel? Detail { get; set; }

    [ObservableProperty]
    public partial string AgentStatus { get; set; }

    partial void OnSelectedActionChanged(ActionItemViewModel? value)
    {
        Detail = value is null
            ? null
            : new ActionDetailViewModel(value, _culture, _agentLauncher, _dispatcher, _confirmation, _parameterFactory);
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
        try
        {
            await _dispatcher.RunOnUiAsync(() => AgentStatus = ViewModelText.Get("Agent_Starting", _culture)).ConfigureAwait(false);
            await _agentLauncher.StartAsync(elevated: false, ct).ConfigureAwait(false);
            // OnAgentStateChanged fires from StartAsync and updates AgentStatus.
        }
        catch (Exception ex)
        {
            await _dispatcher.RunOnUiAsync(() => AgentStatus = ViewModelText.Format("Agent_Error", _culture, ex.Message)).ConfigureAwait(false);
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

    private string FormatStatus(HandshakeResponse? handshake, bool elevated, int runningCount)
    {
        if (handshake is null)
            return ViewModelText.Get("Agent_NotConnected", _culture);

        return ViewModelText.Format(
            elevated ? "Agent_ConnectedElevated" : "Agent_Connected",
            _culture,
            handshake.AgentVersion,
            runningCount);
    }

}
