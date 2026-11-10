using System.Globalization;
using WinEvo.Shell.Core.Services;

namespace WinEvo.Shell.Core.Tests;

public class SystemDriveProviderTests
{
    [Fact]
    public void Null_filter_admits_every_type()
    {
        Assert.True(SystemDriveProvider.IsTypeAllowed("Fixed", allowedTypes: null));
        Assert.True(SystemDriveProvider.IsTypeAllowed("Network", allowedTypes: null));
    }

    [Fact]
    public void Empty_filter_admits_every_type()
    {
        Assert.True(SystemDriveProvider.IsTypeAllowed("Fixed", allowedTypes: []));
    }

    [Fact]
    public void Filter_matches_case_insensitively()
    {
        string[] allowed = ["fixed", "removable"];
        Assert.True(SystemDriveProvider.IsTypeAllowed("Fixed", allowed));
        Assert.True(SystemDriveProvider.IsTypeAllowed("FIXED", allowed));
        Assert.True(SystemDriveProvider.IsTypeAllowed("Removable", allowed));
    }

    [Fact]
    public void Filter_rejects_unlisted_types()
    {
        string[] allowed = ["fixed"];
        Assert.False(SystemDriveProvider.IsTypeAllowed("Network", allowed));
        Assert.False(SystemDriveProvider.IsTypeAllowed("CDRom", allowed));
    }

    [Theory]
    [InlineData("en", "GB free)")]
    [InlineData("fr", "Go libres)")]
    public void Drive_labels_are_written_in_the_requested_language(string language, string expectedEnding)
    {
        // Every Windows machine has at least its system drive, fixed and ready.
        var drives = new SystemDriveProvider(CultureInfo.GetCultureInfo(language)).Enumerate(["fixed"]);

        Assert.NotEmpty(drives);
        Assert.All(drives, drive => Assert.EndsWith(expectedEnding, drive.Label, StringComparison.Ordinal));
    }
}
