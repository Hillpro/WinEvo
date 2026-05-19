using WinEvo.ActionModel;

namespace WinEvo.ActionModel.Tests;

/// <summary>
/// Sanity checks against the manifests actually shipped in the repo's
/// <c>actions/</c> folder. If a manifest starts failing to parse or round-trip,
/// this test catches it before a user runs a broken action.
/// </summary>
public class ShippedManifestsTests
{
    [Fact]
    public async Task Wipe_free_space_parses_drive_param_into_DriveParameter()
    {
        var manifest = await ManifestLoader.LoadAsync(
            ResolvePath("storage/wipe-free-space.json"),
            TestContext.Current.CancellationToken);

        var p = Assert.IsType<DriveParameter>(Assert.Single(manifest.Parameters));
        Assert.Equal("drive", p.Id);
        Assert.True(p.Required);
        Assert.Equal(["fixed", "removable"], p.AllowedDriveTypes!);
        Assert.Equal(ElevationRequirement.Required, manifest.Requirements.Elevation);
    }

    [Fact]
    public async Task Bing_search_results_is_a_toggle_action_with_a_per_parameter_state_probe()
    {
        var manifest = await ManifestLoader.LoadAsync(
            ResolvePath("customization/bing-search-results.json"),
            TestContext.Current.CancellationToken);

        Assert.Equal(InteractionMode.Toggle, manifest.Interaction);
        Assert.Equal(ElevationRequirement.NotRequired, manifest.Requirements.Elevation);
        Assert.Equal(3, manifest.Execution.Steps.Count);

        var p = Assert.IsType<BooleanParameter>(Assert.Single(manifest.Parameters));
        Assert.Equal("bingSearchEnabled", p.Id);
        Assert.True(p.Default!.Value.GetBoolean());

        var state = Assert.IsType<ParameterStateProbe>(p.State);
        Assert.Equal("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Search", state.Key);
        Assert.Equal("BingSearchEnabled", state.Value);
        Assert.Equal("DWORD", state.Type);
        Assert.Equal(1, state.TrueWhen.GetInt32()); // defaulted by the loader
    }

    private static string ResolvePath(string relative)
    {
        // Walk up from the test assembly directory to the repo root, then into
        // actions/. Works regardless of where xUnit runs the test assembly.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WinEvo.slnx")))
            dir = dir.Parent;
        if (dir is null)
            throw new InvalidOperationException("repo root not found from test assembly location");
        return Path.Combine(dir.FullName, "actions", relative);
    }
}
