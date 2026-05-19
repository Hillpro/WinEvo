using System.Text.Json;
using Microsoft.Win32;

namespace WinEvo.Actions.Operations.Tests;

/// <summary>
/// Sandbox key under <c>HKCU\Software\WinEvo.Tests.&lt;Guid&gt;</c> mirrors the
/// pattern used by <see cref="RegistryDeleteOperationTests"/> so each run is
/// isolated and admin is never required.
/// </summary>
public sealed class RegistryReadOperationTests : IDisposable
{
    private readonly string _sandboxSubkey;
    private readonly string _fullPath;

    public RegistryReadOperationTests()
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
    public async Task Reads_DWORD_as_number()
    {
        using (var key = Registry.CurrentUser.CreateSubKey(_sandboxSubkey, writable: true)!)
            key.SetValue("Flag", 1, RegistryValueKind.DWord);

        var op = new RegistryReadOperation { Key = _fullPath, Value = "Flag" };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);
        using var doc = JsonDocument.Parse(result.Message!);
        Assert.True(doc.RootElement.GetProperty("present").GetBoolean());
        Assert.Equal("DWord", doc.RootElement.GetProperty("kind").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("data").GetInt32());
    }

    [Fact]
    public async Task Missing_value_returns_not_present()
    {
        using (var key = Registry.CurrentUser.CreateSubKey(_sandboxSubkey, writable: true)!) { }

        var op = new RegistryReadOperation { Key = _fullPath, Value = "Nope" };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);
        using var doc = JsonDocument.Parse(result.Message!);
        Assert.False(doc.RootElement.GetProperty("present").GetBoolean());
    }

    [Fact]
    public async Task Missing_key_returns_not_present()
    {
        // _sandboxSubkey is never created.
        var op = new RegistryReadOperation { Key = _fullPath, Value = "X" };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);
        using var doc = JsonDocument.Parse(result.Message!);
        Assert.False(doc.RootElement.GetProperty("present").GetBoolean());
    }

    [Fact]
    public async Task Unknown_hive_fails()
    {
        var op = new RegistryReadOperation { Key = "HKXX\\Whatever", Value = "X" };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("unknown hive", result.Error);
    }

    [Fact]
    public void Missing_properties_fail_at_parse_time()
    {
        var ex = Assert.Throws<JsonException>(() => RegistryReadOperation.FromJson(
            JsonDocument.Parse("""{"key":"HKCU\\Foo"}""").RootElement.Clone()));
        Assert.Contains("missing", ex.Message);
    }
}
