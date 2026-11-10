using System.Text.Json;
using WinEvo.ActionModel;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Tests;

/// <summary>
/// Pins what the Shell and the agent rely on for each shipped action: its
/// inputs, its elevation need and the warnings the user must confirm.
/// </summary>
public class CoreActionsTests
{
    [Fact]
    public void Wipe_free_space_asks_for_a_fixed_or_removable_drive()
    {
        var descriptor = new WipeFreeSpaceAction().Descriptor;

        var drive = Assert.IsType<DriveParameter>(Assert.Single(descriptor.Parameters));
        Assert.Equal("drive", drive.Id);
        Assert.True(drive.Required);
        Assert.Equal(["fixed", "removable"], drive.AllowedDriveTypes!);
    }

    [Fact]
    public void Wipe_free_space_requires_elevation_and_warns_before_running()
    {
        var descriptor = new WipeFreeSpaceAction().Descriptor;

        Assert.Equal(ElevationRequirement.Required, descriptor.Elevation);
        Assert.Equal(InteractionMode.Button, descriptor.Interaction);
        Assert.Equal(
            [("storage.wipe.longRunning", WarningSeverity.Warning), ("storage.wipe.destructiveHint", WarningSeverity.Info)],
            descriptor.Warnings.Select(warning => (warning.Key, warning.Severity)));
    }

    [Fact]
    public void Bing_search_results_is_an_unelevated_toggle_that_defaults_to_on()
    {
        var descriptor = new BingSearchResultsAction().Descriptor;

        Assert.Equal(InteractionMode.Toggle, descriptor.Interaction);
        Assert.Equal(ElevationRequirement.NotRequired, descriptor.Elevation);
        Assert.Empty(descriptor.Warnings);

        var isOn = Assert.IsType<BooleanParameter>(Assert.Single(descriptor.Parameters));
        Assert.Equal(ToggleActionDefinition.IsOnParameterId, isOn.Id);
        Assert.Equal(JsonValueKind.True, isOn.Default?.ValueKind);
    }
}
