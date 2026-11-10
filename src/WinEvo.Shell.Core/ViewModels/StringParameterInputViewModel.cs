using System.Globalization;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>
/// Picker for <see cref="StringParameter"/>: renders as a TextBox and emits
/// the raw string. Inputs that deserve a dedicated picker (a wifi profile, a
/// file path, a service name) get their own <see cref="Parameter"/> subclass
/// and view model when that picker ships.
/// </summary>
public sealed partial class StringParameterInputViewModel : ParameterInputViewModel
{
    public StringParameterInputViewModel(StringParameter parameter, CultureInfo culture)
        : base(parameter, culture)
    {
        Value = parameter.Default ?? "";
    }

    [ObservableProperty]
    public partial string Value { get; set; }

    public override bool HasValue => !string.IsNullOrWhiteSpace(Value);

    public override JsonNode? ToJsonValue()
        => string.IsNullOrEmpty(Value) ? null : JsonValue.Create(Value);

    public override void ShowLiveValue(JsonNode liveValue) => Value = liveValue.GetValue<string>();
}
