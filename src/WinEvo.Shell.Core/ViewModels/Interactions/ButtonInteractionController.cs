namespace WinEvo.Shell.Core.ViewModels.Interactions;

/// <summary>
/// Classic "fill parameters, press Execute" mode. The button template binds
/// directly to <see cref="ActionDetailViewModel.ExecuteCommand"/> via
/// <see cref="InteractionController.Detail"/>, so this controller has no
/// additional state of its own — it exists so the XAML
/// <c>DataTemplateSelector</c> has a distinct type to switch on.
/// </summary>
public sealed class ButtonInteractionController : InteractionController
{
    public ButtonInteractionController(ActionDetailViewModel detail) : base(detail)
    {
    }
}
