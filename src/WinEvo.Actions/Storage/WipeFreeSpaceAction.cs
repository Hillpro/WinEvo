using WinEvo.Actions.Abstractions;
using WinEvo.Actions.Operations;

namespace WinEvo.Actions;

/// <summary>
/// Overwrites the free space of a drive so deleted files can no longer be
/// recovered. Existing files are not touched.
/// </summary>
public sealed class WipeFreeSpaceAction : ActionDefinition
{
    private const string DriveParameterId = "drive";

    public override ActionDescriptor Descriptor { get; } = new()
    {
        Id = "storage.wipe-free-space",
        Category = Categories.Storage,
        Name = ActionText.For("WipeFreeSpace_Name"),
        Description = ActionText.For("WipeFreeSpace_Description"),
        Tags = ["privacy", "disk"],
        Icon = "disk-wipe",
        Author = "Hillpro",
        Elevation = ElevationRequirement.Required,
        Warnings =
        [
            new ActionWarning
            {
                Severity = WarningSeverity.Warning,
                Message = ActionText.For("WipeFreeSpace_LongRunningWarning"),
            },
            new ActionWarning
            {
                Severity = WarningSeverity.Info,
                Message = ActionText.For("WipeFreeSpace_DeletedFilesOnlyNote"),
            },
        ],
        Parameters =
        [
            new DriveParameter
            {
                Id = DriveParameterId,
                Name = ActionText.For("WipeFreeSpace_DriveLabel"),
                Required = true,
                AllowedDriveTypes = ["fixed", "removable"],
            },
        ],
    };

    public override Task ExecuteAsync(ActionExecution execution, CancellationToken cancellationToken)
    {
        var drive = execution.GetString(DriveParameterId);

        // No timeout: wiping a large drive legitimately takes hours.
        var wipe = new BuiltinToolOperation { Name = "cipher", Args = [$"/w:{drive}"] };
        return execution.RunAsync(wipe, cancellationToken);
    }
}
