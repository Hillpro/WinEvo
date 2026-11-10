using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using WinEvo.Actions.Abstractions;
using WinEvo.Shell.Core.Services;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>
/// Picker for <see cref="DriveParameter"/>. Populated by the
/// <see cref="Services.IParameterInputFactory"/> from an
/// <see cref="IDriveProvider"/>; the ComboBox binds to
/// <see cref="DriveOptions"/> and <see cref="SelectedDrive"/>. The drive's
/// <see cref="DriveOption.Root"/> is the value the action receives.
/// </summary>
public sealed partial class DriveParameterInputViewModel : ParameterInputViewModel
{
    public DriveParameterInputViewModel(
        DriveParameter parameter,
        CultureInfo culture,
        IReadOnlyList<DriveOption> drives)
        : base(parameter, culture)
    {
        DriveOptions = new ObservableCollection<DriveOption>(drives);
        SelectedDrive = DriveOptions.FirstOrDefault();
    }

    public ObservableCollection<DriveOption> DriveOptions { get; }

    [ObservableProperty]
    public partial DriveOption? SelectedDrive { get; set; }

    public override bool HasValue => SelectedDrive is not null;

    public override JsonNode? ToJsonValue()
        => SelectedDrive is null ? null : JsonValue.Create(SelectedDrive.Root);

    public override void ShowLiveValue(JsonNode liveValue)
    {
        var liveRoot = liveValue.GetValue<string>();
        SelectedDrive = DriveOptions.FirstOrDefault(drive => string.Equals(drive.Root, liveRoot, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"live value '{liveRoot}' of parameter '{Id}' is not an available drive");
    }
}
