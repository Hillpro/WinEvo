using Microsoft.Win32;

namespace WinEvo.Actions.Operations.Tests;

/// <summary>
/// registry-set is the most load-bearing operation in a Windows tweaker.
/// Sandbox under <c>HKCU\Software\WinEvo.Tests.&lt;Guid&gt;</c> mirrors the
/// Delete tests so each run is isolated and admin is never required.
/// </summary>
public sealed class RegistrySetOperationTests : IDisposable
{
    private readonly string _sandboxSubkey;
    private readonly string _fullPath;

    public RegistrySetOperationTests()
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
    public async Task Writes_a_DWORD()
    {
        await Set(RegistrySetOperation.Dword(_fullPath, "V", 5));
        AssertValue(5, RegistryValueKind.DWord);
    }

    [Fact]
    public async Task Writes_a_QWORD_beyond_int32()
    {
        await Set(RegistrySetOperation.Qword(_fullPath, "V", 9000000000L));
        AssertValue(9000000000L, RegistryValueKind.QWord);
    }

    [Fact]
    public async Task Writes_a_plain_string()
    {
        await Set(RegistrySetOperation.Sz(_fullPath, "V", "hello"));
        AssertValue("hello", RegistryValueKind.String);
    }

    [Fact]
    public async Task Writes_an_expand_string_kind()
    {
        await Set(RegistrySetOperation.ExpandSz(_fullPath, "V", "literal"));
        AssertValue("literal", RegistryValueKind.ExpandString);
    }

    private static readonly string[] s_multiValue = ["a", "b"];

    [Fact]
    public async Task Writes_a_multi_string_array()
    {
        await Set(RegistrySetOperation.MultiSz(_fullPath, "V", s_multiValue));
        AssertValue(s_multiValue, RegistryValueKind.MultiString);
    }

    [Fact]
    public async Task Overwrites_an_existing_value_of_another_kind()
    {
        await Set(RegistrySetOperation.Sz(_fullPath, "V", "old"));
        await Set(RegistrySetOperation.Dword(_fullPath, "V", 1));
        AssertValue(1, RegistryValueKind.DWord);
    }

    [Fact]
    public async Task Reports_what_was_written()
    {
        var result = await RegistrySetOperation.Dword(_fullPath, "V", 1)
            .ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.Equal($"HKCU\\{_sandboxSubkey}\\V = 1 (DWord)", result.Message);
    }

    [Fact]
    public async Task Unknown_hive_fails()
    {
        var result = await RegistrySetOperation.Dword("HKXX\\Whatever", "V", 1)
            .ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("unknown hive", result.Error);
    }

    // --- helpers ---

    private static async Task Set(RegistrySetOperation operation)
    {
        var result = await operation.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);
        Assert.True(result.Success, result.Error);
    }

    private void AssertValue(object expected, RegistryValueKind expectedKind)
    {
        using var key = Registry.CurrentUser.OpenSubKey(_sandboxSubkey);
        Assert.NotNull(key);
        Assert.Equal(expectedKind, key!.GetValueKind("V"));
        Assert.Equal(expected, key.GetValue("V"));
    }
}
