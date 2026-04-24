using System.Text.Json;
using Microsoft.Win32;

namespace WinEvo.Actions.Operations.Tests;

/// <summary>
/// Uses a sandbox key under <c>HKCU\Software\WinEvo.Tests.&lt;Guid&gt;</c> so
/// each run is isolated and any orphaned subtree from a prior crash can be
/// spotted by name. HKCU is chosen over HKLM so the tests don't need admin.
/// </summary>
public sealed class RegistryDeleteOperationTests : IDisposable
{
    private readonly string _sandboxSubkey;
    private readonly string _fullPath;

    public RegistryDeleteOperationTests()
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
    public async Task Deletes_a_single_value()
    {
        using (var key = Registry.CurrentUser.CreateSubKey(_sandboxSubkey, writable: true)!)
        {
            key.SetValue("Keep", 1, RegistryValueKind.DWord);
            key.SetValue("Remove", 2, RegistryValueKind.DWord);
        }

        var op = new RegistryDeleteOperation { Key = _fullPath, ValueName = "Remove" };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);

        using var after = Registry.CurrentUser.OpenSubKey(_sandboxSubkey);
        Assert.NotNull(after);
        Assert.Equal(1, after!.GetValue("Keep"));
        Assert.Null(after.GetValue("Remove"));
    }

    [Fact]
    public async Task Deleting_missing_value_is_idempotent()
    {
        using (var key = Registry.CurrentUser.CreateSubKey(_sandboxSubkey, writable: true)!) { }

        var op = new RegistryDeleteOperation { Key = _fullPath, ValueName = "NeverWas" };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public async Task Deletes_a_key_subtree()
    {
        using (var key = Registry.CurrentUser.CreateSubKey(_sandboxSubkey + "\\Nested\\Deeper", writable: true)!)
        {
            key.SetValue("V", "deep");
        }

        var op = new RegistryDeleteOperation { Key = _fullPath, ValueName = null };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);
        Assert.Null(Registry.CurrentUser.OpenSubKey(_sandboxSubkey));
    }

    [Fact]
    public async Task Deleting_missing_key_is_idempotent()
    {
        // _sandboxSubkey was never created.
        var op = new RegistryDeleteOperation { Key = _fullPath, ValueName = null };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public async Task Refuses_to_delete_hive_root()
    {
        var op = new RegistryDeleteOperation { Key = "HKCU", ValueName = null };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("hive root", result.Error);
    }

    [Fact]
    public async Task Unknown_hive_fails()
    {
        var op = new RegistryDeleteOperation { Key = "HKXX\\Whatever", ValueName = null };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("unknown hive", result.Error);
    }

    [Fact]
    public void Missing_key_property_fails_at_parse_time()
    {
        var ex = Assert.Throws<JsonException>(() => RegistryDeleteOperation.FromJson(
            JsonDocument.Parse("""{}""").RootElement.Clone()));
        Assert.Contains("missing", ex.Message);
    }
}
