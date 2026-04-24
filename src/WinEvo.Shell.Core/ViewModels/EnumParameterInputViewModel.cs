using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using WinEvo.ActionModel;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>
/// Picker for <see cref="EnumParameter"/> — renders as a <c>ComboBox</c> of
/// the manifest's <see cref="EnumParameter.Choices"/>. The emitted value is
/// the selected choice string.
/// </summary>
public sealed partial class EnumParameterInputViewModel : ParameterInputViewModel
{
    public EnumParameterInputViewModel(EnumParameter parameter, string? language)
        : base(parameter, language)
    {
        Choices = new ObservableCollection<string>(parameter.Choices);

        var preferred = parameter.Default is { } def && def.ValueKind == JsonValueKind.String
            ? def.GetString()
            : null;
        SelectedChoice = preferred is not null && Choices.Contains(preferred)
            ? preferred
            : Choices.FirstOrDefault();
    }

    public ObservableCollection<string> Choices { get; }

    [ObservableProperty]
    public partial string? SelectedChoice { get; set; }

    public override bool HasValue => !string.IsNullOrEmpty(SelectedChoice);

    public override JsonNode? ToJsonValue()
        => string.IsNullOrEmpty(SelectedChoice) ? null : JsonValue.Create(SelectedChoice);
}
