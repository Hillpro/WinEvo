using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using WinEvo.ActionModel;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>
/// Base for a single action parameter's input VM. Concrete subclasses own the
/// type-specific storage and widget-binding surface; a
/// <see cref="Services.IParameterInputFactory"/> picks the right subclass per
/// <see cref="Parameter.Type"/>. Matching <c>DataTemplate</c>s in the Shell are
/// selected at render time by <c>ParameterInputTemplateSelector</c>.
/// </summary>
public abstract class ParameterInputViewModel : ObservableObject
{
    protected ParameterInputViewModel(Parameter parameter, string? language)
    {
        Parameter = parameter;
        DisplayName = parameter.Name ?? parameter.Id;
        DisplayDescription = parameter.Description;
        _ = language; // Localization hook — per-parameter overrides resolved by the factory caller if needed.
    }

    public Parameter Parameter { get; }
    public string Id => Parameter.Id;
    public string Type => Parameter.Type;
    public bool Required => Parameter.Required;
    public string DisplayName { get; }
    public string? DisplayDescription { get; }

    /// <summary>
    /// True when the user has supplied a usable value. The <c>Execute</c>
    /// button uses this to surface missing-required warnings.
    /// </summary>
    public abstract bool HasValue { get; }

    /// <summary>Serialise the current input to the IPC wire format.</summary>
    public abstract JsonNode? ToJsonValue();
}
