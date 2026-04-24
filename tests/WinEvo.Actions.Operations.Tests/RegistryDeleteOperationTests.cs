using System.Text.Json;
using Microsoft.Win32;
using WinEvo.ActionModel;
using WinEvo.Actions.Abstractions;

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

        var result = await new RegistryDeleteOperation().ExecuteAsync(
            Context(new { operation = "registry-delete", key = _fullPath, value = "Remove" }),
            TestContext.Current.CancellationToken);

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

        var result = await new RegistryDeleteOperation().ExecuteAsync(
            Context(new { operation = "registry-delete", key = _fullPath, value = "NeverWas" }),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public async Task Deletes_a_key_subtree()
    {
        using (var key = Registry.CurrentUser.CreateSubKey(_sandboxSubkey + "\\Nested\\Deeper", writable: true)!)
        {
            key.SetValue("V", "deep");
        }

        var result = await new RegistryDeleteOperation().ExecuteAsync(
            Context(new { operation = "registry-delete", key = _fullPath }),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);
        Assert.Null(Registry.CurrentUser.OpenSubKey(_sandboxSubkey));
    }

    [Fact]
    public async Task Deleting_missing_key_is_idempotent()
    {
        // _sandboxSubkey was never created.
        var result = await new RegistryDeleteOperation().ExecuteAsync(
            Context(new { operation = "registry-delete", key = _fullPath }),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public async Task Refuses_to_delete_hive_root()
    {
        var result = await new RegistryDeleteOperation().ExecuteAsync(
            Context(new { operation = "registry-delete", key = "HKCU" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("hive root", result.Error);
    }

    [Fact]
    public async Task Unknown_hive_fails()
    {
        var result = await new RegistryDeleteOperation().ExecuteAsync(
            Context(new { operation = "registry-delete", key = "HKXX\\Whatever" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("unknown hive", result.Error);
    }

    [Fact]
    public async Task Missing_key_property_fails()
    {
        var result = await new RegistryDeleteOperation().ExecuteAsync(
            Context(new { operation = "registry-delete" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("missing", result.Error);
    }

    private static OperationContext Context(object properties)
    {
        var json = JsonSerializer.Serialize(properties);
        var root = JsonDocument.Parse(json).RootElement.Clone();
        return new OperationContext
        {
            Step = new OperationStep
            {
                Operation = root.GetProperty("operation").GetString()!,
                Properties = root,
            },
            Parameters = new Dictionary<string, object?>(),
        };
    }
}
