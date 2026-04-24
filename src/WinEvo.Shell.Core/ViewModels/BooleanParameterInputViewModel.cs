using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using WinEvo.ActionModel;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>
/// Picker for <see cref="BooleanParameter"/> — renders as a <c>ToggleSwitch</c>.
/// Booleans always have a value (on or off), so <see cref="HasValue"/> is
/// always true.
/// </summary>
public sealed partial class BooleanParameterInputViewModel : ParameterInputViewModel
{
    public BooleanParameterInputViewModel(BooleanParameter parameter, string? language)
        : base(parameter, language)
    {
        Value = parameter.Default is { } def && (def.ValueKind == JsonValueKind.True || def.ValueKind == JsonValueKind.False)
            ? def.GetBoolean()
            : false;
    }

    [ObservableProperty]
    public partial bool Value { get; set; }

    public override bool HasValue => true;

    public override JsonNode? ToJsonValue() => JsonValue.Create(Value);
}
