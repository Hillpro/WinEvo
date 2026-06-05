using System.Text.Json;
using Microsoft.Win32;

namespace WinEvo.Actions.Operations.Tests;

/// <summary>
/// registry-set is the most load-bearing operation in a Windows tweaker and
/// carries the most branching coercion logic (JSON number / true / false /
/// templated-string -> int, per width). Sandbox under
/// <c>HKCU\Software\WinEvo.Tests.&lt;Guid&gt;</c> mirrors the Read/Delete tests
/// so each run is isolated and admin is never required.
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
    public async Task Writes_DWORD_from_a_json_number()
    {
        await Set("DWORD", Json("5"));
        AssertValue(5, RegistryValueKind.DWord);
    }

    [Theory]
    [InlineData("true", 1)]
    [InlineData("false", 0)]
    public async Task Writes_DWORD_from_a_json_boolean(string json, int expected)
    {
        await Set("DWORD", Json(json));
        AssertValue(expected, RegistryValueKind.DWord);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task Writes_DWORD_from_a_templated_boolean_parameter(bool flag, int expected)
    {
        // The exact toggle round-trip: bool param -> "true"/"false" (Templating) -> 1/0 (ParseInt32).
        await Set("DWORD", Json("\"{{params.flag}}\""), parameters: new Dictionary<string, object?> { ["flag"] = flag });
        AssertValue(expected, RegistryValueKind.DWord);
    }

    [Fact]
    public async Task Writes_QWORD_from_a_number_beyond_int32()
    {
        await Set("QWORD", Json("9000000000"));
        AssertValue(9000000000L, RegistryValueKind.QWord);
    }

    [Fact]
    public async Task Writes_a_plain_string()
    {
        await Set("STRING", Json("\"hello\""));
        AssertValue("hello", RegistryValueKind.String);
    }

    [Fact]
    public async Task Writes_an_expand_string_kind()
    {
        await Set("EXPAND_STRING", Json("\"literal\""));
        AssertValue("literal", RegistryValueKind.ExpandString);
    }

    [Fact]
    public async Task Writes_a_templated_string()
    {
        await Set("STRING", Json("\"{{params.s}}\""), parameters: new Dictionary<string, object?> { ["s"] = "world" });
        AssertValue("world", RegistryValueKind.String);
    }

    private static readonly string[] s_multiValue = ["a", "b"];

    [Fact]
    public async Task Writes_a_multi_string_array()
    {
        await Set("MULTI_STRING", Json("""["a","b"]"""));
        AssertValue(s_multiValue, RegistryValueKind.MultiString);
    }

    [Fact]
    public async Task Unsupported_type_fails()
    {
        var result = await Run("FOO", Json("1"));
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Unknown_hive_fails()
    {
        var op = new RegistrySetOperation { Key = "HKXX\\Whatever", Value = "V", DataType = "DWORD", Data = Json("1") };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("unknown hive", result.Error);
    }

    [Fact]
    public void Missing_data_fails_at_parse_time()
    {
        var ex = Assert.Throws<JsonException>(() => RegistrySetOperation.FromJson(
            JsonDocument.Parse("""{"key":"HKCU\\Foo","value":"V","type":"DWORD"}""").RootElement.Clone()));
        Assert.Contains("data", ex.Message);
    }

    [Fact]
    public void Missing_key_fails_at_parse_time()
    {
        var ex = Assert.Throws<JsonException>(() => RegistrySetOperation.FromJson(
            JsonDocument.Parse("""{"value":"V","type":"DWORD","data":1}""").RootElement.Clone()));
        Assert.Contains("key", ex.Message);
    }

    // --- helpers ---

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    private Task<Abstractions.OperationResult> Run(string type, JsonElement data, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        var op = new RegistrySetOperation { Key = _fullPath, Value = "V", DataType = type, Data = data };
        var context = parameters is null
            ? OperationTestContext.Empty()
            : OperationTestContext.WithParameters(parameters);
        return op.ExecuteAsync(context, TestContext.Current.CancellationToken);
    }

    private async Task Set(string type, JsonElement data, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        var result = await Run(type, data, parameters);
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
