using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Nodes;
using WinEvo.ActionModel;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>
/// Holds the user-entered value for one action parameter. Every parameter is
/// currently rendered as a text field in the UI. TODO: type-specific pickers
/// (drive, wifi-profile, enum dropdown, boolean toggle, etc.).
/// </summary>
public sealed partial class ParameterInputViewModel : ObservableObject
{
    public ParameterInputViewModel(Parameter parameter, string? language)
    {
        Parameter = parameter;
        DisplayName = ResolveName(parameter, language);
        DisplayDescription = ResolveDescription(parameter, language);
        Value = parameter.Default is { } def && def.ValueKind != System.Text.Json.JsonValueKind.Null
            ? def.ToString()
            : "";
    }

    public Parameter Parameter { get; }
    public string DisplayName { get; }
    public string? DisplayDescription { get; }
    public string Id => Parameter.Id;
    public string Type => Parameter.Type;
    public bool Required => Parameter.Required;

    [ObservableProperty]
    public partial string Value { get; set; }

    /// <summary>Converts the current text value into a <see cref="JsonNode"/> appropriate for the parameter's type.</summary>
    public JsonNode? ToJsonValue()
    {
        if (string.IsNullOrEmpty(Value))
            return null;

        return Type switch
        {
            "integer" => long.TryParse(Value, out var i) ? JsonValue.Create(i) : JsonValue.Create(Value),
            "boolean" => JsonValue.Create(Value.Equals("true", StringComparison.OrdinalIgnoreCase)),
            _ => JsonValue.Create(Value),
        };
    }

    private static string ResolveName(Parameter p, string? lang)
    {
        // LocalizationEntry.parameters is parent-scoped; per-parameter name overrides
        // are resolved against the owning manifest's Localization map by the caller
        // when it has access. Here we fall back to the base English name.
        return p.Name ?? p.Id;
    }

    private static string? ResolveDescription(Parameter p, string? lang) => p.Description;
}
