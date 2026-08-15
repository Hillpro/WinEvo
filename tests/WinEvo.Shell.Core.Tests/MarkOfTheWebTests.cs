using WinEvo.Shell.Core.Services;

namespace WinEvo.Shell.Core.Tests;

public class MarkOfTheWebTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"winevo-motw-{Guid.NewGuid():N}.tmp");

    public MarkOfTheWebTests() => File.WriteAllText(_file, "payload");

    public void Dispose()
    {
        try { File.Delete(_file); } catch { /* best-effort cleanup */ }
        GC.SuppressFinalize(this);
    }

    private void StampDownloadMarker()
        => File.WriteAllText($"{_file}:Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n");

    private bool HasDownloadMarker() => File.Exists($"{_file}:Zone.Identifier");

    [Fact]
    public void Reports_NotPresent_for_a_file_that_was_never_downloaded()
    {
        Assert.Equal(MarkOfTheWebState.NotPresent, MarkOfTheWeb.Clear(_file));
    }

    [Fact]
    public void Removes_the_marker_and_reports_Cleared()
    {
        StampDownloadMarker();
        Assert.True(HasDownloadMarker());

        Assert.Equal(MarkOfTheWebState.Cleared, MarkOfTheWeb.Clear(_file));
        Assert.False(HasDownloadMarker());
    }

    [Fact]
    public void Leaves_the_file_itself_intact()
    {
        StampDownloadMarker();

        MarkOfTheWeb.Clear(_file);

        Assert.True(File.Exists(_file));
        Assert.Equal("payload", File.ReadAllText(_file));
    }

    [Fact]
    public void Is_idempotent()
    {
        StampDownloadMarker();

        Assert.Equal(MarkOfTheWebState.Cleared, MarkOfTheWeb.Clear(_file));
        Assert.Equal(MarkOfTheWebState.NotPresent, MarkOfTheWeb.Clear(_file));
    }

    [Fact]
    public void Rejects_a_blank_path()
    {
        Assert.Throws<ArgumentException>(() => MarkOfTheWeb.Clear("   "));
    }
}
