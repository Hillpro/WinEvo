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
    public async Task Disable_bing_has_no_parameters()
    {
        var manifest = await ManifestLoader.LoadAsync(
            ResolvePath("customization/disable-bing-in-search.json"),
            TestContext.Current.CancellationToken);

        Assert.Empty(manifest.Parameters);
        Assert.Equal(ElevationRequirement.NotRequired, manifest.Requirements.Elevation);
        Assert.Equal(3, manifest.Execution.Steps.Count);
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
