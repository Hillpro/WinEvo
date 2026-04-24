using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using WinEvo.ActionModel;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>
/// Picker for <see cref="StringParameter"/> — the fallback shape for every
/// parameter type that doesn't (yet) have a dedicated widget. Renders as a
/// TextBox; emits the raw string. Types like <c>wifi-profile</c>,
/// <c>file-path</c>, and <c>service-name</c> land here today and should move
/// to their own subclass once a proper picker ships.
/// </summary>
public sealed partial class StringParameterInputViewModel : ParameterInputViewModel
{
    public StringParameterInputViewModel(Parameter parameter, string? language)
        : base(parameter, language)
    {
        Value = parameter.Default is { } def && def.ValueKind == JsonValueKind.String
            ? def.GetString() ?? ""
            : "";
    }

    [ObservableProperty]
    public partial string Value { get; set; }

    public override bool HasValue => !string.IsNullOrWhiteSpace(Value);

    public override JsonNode? ToJsonValue()
        => string.IsNullOrEmpty(Value) ? null : JsonValue.Create(Value);
}
