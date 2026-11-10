using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using WinEvo.ActionModel;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>
/// Picker for <see cref="BooleanParameter"/> — renders as a <c>ToggleSwitch</c>.
/// Booleans always have a value, so <see cref="HasValue"/> is always true.
///
/// <para>
/// The value can change for two reasons, and only one of them is the user:
/// a flip in the UI raises <see cref="UserChangedValue"/>, while
/// <see cref="ShowLiveValue"/> does not. That lets
/// <see cref="Interactions.ToggleInteractionController"/> distinguish "Windows
/// currently looks like this" from "the user just flipped me, run the action."
/// </para>
/// </summary>
public sealed partial class BooleanParameterInputViewModel : ParameterInputViewModel
{
    private bool _value;

    public BooleanParameterInputViewModel(BooleanParameter parameter, CultureInfo culture)
        : base(parameter, culture)
    {
        _value = parameter.Default is { } def
            && (def.ValueKind == JsonValueKind.True || def.ValueKind == JsonValueKind.False)
            && def.GetBoolean();
    }

    /// <summary>
    /// Toggle value. Setting this via XAML's TwoWay binding raises
    /// <see cref="UserChangedValue"/>; <see cref="ShowLiveValue"/> updates the
    /// backing field directly and does not.
    /// </summary>
    public bool Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            OnPropertyChanged();
            UserChangedValue?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Raised when <see cref="Value"/> is changed via the public setter
    /// (i.e. XAML two-way binding), never by <see cref="ShowLiveValue"/>.
    /// </summary>
    public event EventHandler? UserChangedValue;

    public override bool HasValue => true;

    public override JsonNode? ToJsonValue() => JsonValue.Create(Value);

    public override void ShowLiveValue(JsonNode liveValue)
    {
        var isOn = liveValue.GetValue<bool>();
        if (_value == isOn) return;
        _value = isOn;
        OnPropertyChanged(nameof(Value));
    }
}
