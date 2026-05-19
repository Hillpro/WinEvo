using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WinEvo.Shell.Core.ViewModels.Interactions;

/// <summary>
/// Stateful on/off mode. Binds a <c>ToggleSwitch</c> to the action's single
/// boolean parameter and fires <see cref="ActionDetailViewModel.ExecuteCommand"/>
/// whenever the user flips it. Hydration of the initial value happens on the
/// parameter VM itself via <see cref="Services.IParameterStateLoader"/>; the
/// controller only forwards <see cref="IsLoadingState"/> for the UI and gates
/// <see cref="IsInteractive"/> off both that and the action's running state.
/// Subscribes to <see cref="BooleanParameterInputViewModel.UserChangedValue"/>
/// (not generic <c>PropertyChanged</c>) so hydration writes do NOT trigger an
/// execute — the event is raised only when the public setter is hit, i.e. by
/// XAML's two-way binding.
/// </summary>
public sealed class ToggleInteractionController : InteractionController, IDisposable
{
    private readonly BooleanParameterInputViewModel _toggleParameter;

    public ToggleInteractionController(ActionDetailViewModel detail) : base(detail)
    {
        _toggleParameter = detail.Parameters.OfType<BooleanParameterInputViewModel>().SingleOrDefault()
            ?? throw new InvalidOperationException(
                $"action '{detail.Item.Manifest.Id}' is interaction:toggle but does not declare a single boolean parameter");

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
    public bool IsLoadingState => _toggleParameter.IsLoadingState;

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
        switch (e.PropertyName)
        {
            case nameof(BooleanParameterInputViewModel.Value):
                OnPropertyChanged(nameof(IsOn));
                break;
            case nameof(BooleanParameterInputViewModel.IsLoadingState):
                OnPropertyChanged(nameof(IsLoadingState));
                OnPropertyChanged(nameof(IsInteractive));
                break;
        }
    }

    private void OnDetailPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ActionDetailViewModel.IsRunning))
            OnPropertyChanged(nameof(IsInteractive));
    }

    public void Dispose()
    {
        _toggleParameter.UserChangedValue -= OnUserChangedValue;
        _toggleParameter.PropertyChanged -= OnParameterPropertyChanged;
        Detail.PropertyChanged -= OnDetailPropertyChanged;
    }
}
