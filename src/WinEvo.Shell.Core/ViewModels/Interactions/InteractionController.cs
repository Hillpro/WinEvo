using CommunityToolkit.Mvvm.ComponentModel;

namespace WinEvo.Shell.Core.ViewModels.Interactions;

/// <summary>
/// Owns the per-mode UI glue between a selected action's
/// <see cref="ActionDetailViewModel"/> and the surface that drives it
/// (a button, a toggle, a slider in the future, …). Each subclass exposes
/// the bindings its template needs; the Shell picks the template by the
/// controller's runtime type. Adding a new interaction mode = new subclass +
/// new <c>DataTemplate</c> + one switch arm where the controller is constructed.
/// </summary>
public abstract class InteractionController : ObservableObject
{
    protected InteractionController(ActionDetailViewModel detail)
    {
        Detail = detail;
    }

    /// <summary>The action this controller drives. Templates use it to reach Parameters, IsRunning, etc.</summary>
    public ActionDetailViewModel Detail { get; }
}
