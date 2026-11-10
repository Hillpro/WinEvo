using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WinEvo.Shell.Core.ViewModels.Interactions;

/// <summary>
/// Stateful on/off mode. Binds a <c>ToggleSwitch</c> to the action's single
/// boolean parameter and fires <see cref="ActionDetailViewModel.ExecuteCommand"/>
/// whenever the user flips it. The switch's initial position is the live
/// system state, which <see cref="ActionDetailViewModel"/> reads from the
/// agent; the controller keeps the switch disabled until that read is over
/// and while the action runs.
/// Subscribes to <see cref="BooleanParameterInputViewModel.UserChangedValue"/>
/// (not generic <c>PropertyChanged</c>) so showing the live state does NOT
/// trigger an execute — the event is raised only when the public setter is
/// hit, i.e. by XAML's two-way binding.
/// </summary>
public sealed class ToggleInteractionController : InteractionController, IDisposable
{
    private readonly BooleanParameterInputViewModel _toggleParameter;

    public ToggleInteractionController(ActionDetailViewModel detail) : base(detail)
    {
        // A toggle action's only parameter is its switch; ToggleActionDefinition
        // builds the descriptor that way.
        _toggleParameter = detail.Parameters.OfType<BooleanParameterInputViewModel>().Single();

        _toggleParameter.UserChangedValue += OnUserChangedValue;
        _toggleParameter.PropertyChanged += OnParameterPropertyChanged;
        detail.PropertyChanged += OnDetailPropertyChanged;
    }

    /// <summary>Two-way: the ToggleSwitch's IsOn binds here.</summary>
    public bool IsOn
    {
        get => _toggleParameter.Value;
        set => _toggleParameter.Value = value;
    }

    /// <summary>True while the initial-state read is in flight.</summary>
    public bool IsLoadingState => Detail.IsReadingLiveState;

    /// <summary>True when the toggle should accept user input: not running, not loading.</summary>
    public bool IsInteractive => !Detail.IsRunning && !IsLoadingState;

    private void OnUserChangedValue(object? sender, EventArgs e)
    {
        if (Detail.IsRunning) return;
        if (Detail.ExecuteCommand.CanExecute(null))
            Detail.ExecuteCommand.Execute(null);
    }

    private void OnParameterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BooleanParameterInputViewModel.Value))
            OnPropertyChanged(nameof(IsOn));
    }

    private void OnDetailPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ActionDetailViewModel.IsRunning):
                OnPropertyChanged(nameof(IsInteractive));
                break;
            case nameof(ActionDetailViewModel.IsReadingLiveState):
                OnPropertyChanged(nameof(IsLoadingState));
                OnPropertyChanged(nameof(IsInteractive));
                break;
        }
    }

    public void Dispose()
    {
        _toggleParameter.UserChangedValue -= OnUserChangedValue;
        _toggleParameter.PropertyChanged -= OnParameterPropertyChanged;
        Detail.PropertyChanged -= OnDetailPropertyChanged;
    }
}
