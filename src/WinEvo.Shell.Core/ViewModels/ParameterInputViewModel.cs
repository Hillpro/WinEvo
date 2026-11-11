using System.Globalization;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>
/// Base for a single action parameter's input VM. Concrete subclasses own the
/// type-specific storage and widget-binding surface; a
/// <see cref="Services.IParameterInputFactory"/> picks the right subclass per
/// <see cref="Parameter"/> subclass. Matching <c>DataTemplate</c>s in the Shell are
/// selected at render time by <c>ParameterInputTemplateSelector</c>.
/// </summary>
public abstract class ParameterInputViewModel : ObservableObject
{
    protected ParameterInputViewModel(Parameter parameter, CultureInfo culture)
    {
        Parameter = parameter;
        DisplayName = parameter.Name?.Resolve(culture) ?? parameter.Id;
        DisplayDescription = parameter.Description?.Resolve(culture);
    }

    public Parameter Parameter { get; }
    public string Id => Parameter.Id;
    public bool Required => Parameter.Required;
    public string DisplayName { get; }
    public string? DisplayDescription { get; }
    public bool HasDescription => !string.IsNullOrWhiteSpace(DisplayDescription);

    /// <summary>
    /// True when the user has supplied a usable value. The <c>Execute</c>
    /// button uses this to surface missing-required warnings.
    /// </summary>
    public abstract bool HasValue { get; }

    /// <summary>Serialise the current input to the IPC wire format.</summary>
    public abstract JsonNode? ToJsonValue();

    /// <summary>
    /// Displays the value currently in effect on the system, as read by the
    /// agent, in place of the declared default. This is not user input: it
    /// must not trigger anything a user edit would.
    /// </summary>
    public abstract void ShowLiveValue(JsonNode liveValue);
}
