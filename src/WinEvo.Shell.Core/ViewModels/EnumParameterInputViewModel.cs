using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>
/// Picker for <see cref="EnumParameter"/> — renders as a <c>ComboBox</c> of
/// the parameter's <see cref="EnumParameter.Choices"/>. The emitted value is
/// the selected choice string.
/// </summary>
public sealed partial class EnumParameterInputViewModel : ParameterInputViewModel
{
    public EnumParameterInputViewModel(EnumParameter parameter, CultureInfo culture)
        : base(parameter, culture)
    {
        Choices = new ObservableCollection<string>(parameter.Choices);

        SelectedChoice = parameter.Default is { } preferred && Choices.Contains(preferred)
            ? preferred
            : Choices.FirstOrDefault();
    }

    public ObservableCollection<string> Choices { get; }

    [ObservableProperty]
    public partial string? SelectedChoice { get; set; }

    public override bool HasValue => !string.IsNullOrEmpty(SelectedChoice);

    public override JsonNode? ToJsonValue()
        => string.IsNullOrEmpty(SelectedChoice) ? null : JsonValue.Create(SelectedChoice);

    public override void ShowLiveValue(JsonNode liveValue)
    {
        var liveChoice = liveValue.GetValue<string>();
        if (!Choices.Contains(liveChoice))
            throw new InvalidOperationException($"live value '{liveChoice}' of parameter '{Id}' is not one of its choices");
        SelectedChoice = liveChoice;
    }
}
