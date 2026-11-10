using System.Globalization;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>
/// Picker for <see cref="IntegerParameter"/> — renders as a
/// <c>NumberBox</c> honoring the parameter's <see cref="IntegerParameter.Min"/>
/// and <see cref="IntegerParameter.Max"/>. <c>NaN</c> represents "not supplied"
/// for required-parameter checks.
/// </summary>
public sealed partial class IntegerParameterInputViewModel : ParameterInputViewModel
{
    public IntegerParameterInputViewModel(IntegerParameter parameter, CultureInfo culture)
        : base(parameter, culture)
    {
        IntegerParameter = parameter;
        Value = parameter.Default ?? double.NaN;
    }

    public IntegerParameter IntegerParameter { get; }

    public double Minimum => IntegerParameter.Min ?? double.MinValue;
    public double Maximum => IntegerParameter.Max ?? double.MaxValue;

    [ObservableProperty]
    public partial double Value { get; set; }

    public override bool HasValue => !double.IsNaN(Value);

    public override JsonNode? ToJsonValue()
        => HasValue ? JsonValue.Create((long)Value) : null;

    public override void ShowLiveValue(JsonNode liveValue) => Value = liveValue.GetValue<long>();
}
