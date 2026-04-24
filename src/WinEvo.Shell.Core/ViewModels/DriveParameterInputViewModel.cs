using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using WinEvo.ActionModel;
using WinEvo.Shell.Core.Services;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>
/// Picker for <c>drive</c> parameters. Populated by the
/// <see cref="Services.IParameterInputFactory"/> from an
/// <see cref="IDriveProvider"/>; the ComboBox binds to
/// <see cref="DriveOptions"/> and <see cref="SelectedDrive"/>. The drive's
/// <see cref="DriveOption.Root"/> is the value substituted into the manifest.
/// </summary>
public sealed partial class DriveParameterInputViewModel : ParameterInputViewModel
{
    public DriveParameterInputViewModel(
        Parameter parameter,
        string? language,
        IReadOnlyList<DriveOption> drives)
        : base(parameter, language)
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
}
