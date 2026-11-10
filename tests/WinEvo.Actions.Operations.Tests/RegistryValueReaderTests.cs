using Microsoft.Win32;

namespace WinEvo.Actions.Operations.Tests;

/// <summary>
/// Same sandbox pattern as <see cref="RegistryReadOperationTests"/>: a
/// throwaway key under <c>HKCU\Software\WinEvo.Tests.&lt;Guid&gt;</c>.
/// </summary>
public sealed class RegistryValueReaderTests : IDisposable
{
    private readonly string _sandboxSubkey;
    private readonly string _fullPath;

    public RegistryValueReaderTests()
    {
        _sandboxSubkey = $"Software\\WinEvo.Tests.{Guid.NewGuid():N}";
        _fullPath = $"HKCU\\{_sandboxSubkey}";
    }

    public void Dispose()
    {
        try { Registry.CurrentUser.DeleteSubKeyTree(_sandboxSubkey, throwOnMissingSubKey: false); }
        catch { /* best-effort cleanup */ }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Reads_back_what_a_DWORD_write_stored()
    {
        var write = RegistrySetOperation.Dword(_fullPath, "Flag", 1);
        var result = await write.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);
        Assert.True(result.Success, result.Error);

        Assert.Equal(1, RegistryValueReader.Read(_fullPath, "Flag"));
    }

    [Fact]
    public void Reads_a_string_value_as_string()
    {
        using (var key = Registry.CurrentUser.CreateSubKey(_sandboxSubkey, writable: true)!)
            key.SetValue("Name", "WinEvo", RegistryValueKind.String);

        Assert.Equal("WinEvo", RegistryValueReader.Read(_fullPath, "Name"));
    }

    [Fact]
    public void Missing_value_reads_as_null()
    {
        using (Registry.CurrentUser.CreateSubKey(_sandboxSubkey, writable: true)) { }

        Assert.Null(RegistryValueReader.Read(_fullPath, "Nope"));
    }

    [Fact]
    public void Missing_key_reads_as_null()
    {
        // _sandboxSubkey is never created.
        Assert.Null(RegistryValueReader.Read(_fullPath, "X"));
    }

    [Fact]
    public void Unknown_hive_is_rejected()
    {
        var thrown = Assert.Throws<ArgumentException>(() => RegistryValueReader.Read("HKXX\\Whatever", "X"));

        Assert.Contains("unknown hive", thrown.Message);
    }
}
