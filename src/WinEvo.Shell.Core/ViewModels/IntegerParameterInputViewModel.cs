using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using WinEvo.ActionModel;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>
/// Picker for <see cref="IntegerParameter"/> — renders as a
/// <c>NumberBox</c> honoring the manifest's <see cref="IntegerParameter.Min"/>
/// and <see cref="IntegerParameter.Max"/>. <c>NaN</c> represents "not supplied"
/// for required-parameter checks.
/// </summary>
public sealed partial class IntegerParameterInputViewModel : ParameterInputViewModel
{
    public IntegerParameterInputViewModel(IntegerParameter parameter, string? language)
        : base(parameter, language)
    {
        IntegerParameter = parameter;
        Value = parameter.Default is { } def && def.ValueKind == JsonValueKind.Number && def.TryGetInt64(out var i)
            ? i
            : double.NaN;
    }

    public IntegerParameter IntegerParameter { get; }

    public double Minimum => IntegerParameter.Min ?? double.MinValue;
    public double Maximum => IntegerParameter.Max ?? double.MaxValue;

    [ObservableProperty]
    public partial double Value { get; set; }

    public override bool HasValue => !double.IsNaN(Value);

    public override JsonNode? ToJsonValue()
        => HasValue ? JsonValue.Create((long)Value) : null;
}
