using System.Text.Json;
using WinEvo.ActionModel;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations.Tests;

public class BuiltinToolOperationTests
{
    [Fact]
    public async Task Runs_a_known_system32_tool_with_explicit_exe()
    {
        // whoami.exe is present on every supported Windows build.
        var result = await new BuiltinToolOperation().ExecuteAsync(
            Context("""{"operation":"builtin-tool","name":"whoami.exe"}"""),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error ?? result.Message);
    }

    [Fact]
    public async Task Accepts_tool_name_without_exe_suffix()
    {
        // Like cmd.exe, ".exe" is inferred when the caller omits it.
        var result = await new BuiltinToolOperation().ExecuteAsync(
            Context("""{"operation":"builtin-tool","name":"whoami"}"""),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error ?? result.Message);
    }

    [Theory]
    [InlineData(@"C:\Windows\System32\whoami.exe")]
    [InlineData(@"..\..\Program Files\evil.exe")]
    [InlineData(@"subdir\whoami.exe")]
    public async Task Rejects_non_bare_names(string name)
    {
        var json = JsonSerializer.Serialize(new { operation = "builtin-tool", name });
        var result = await new BuiltinToolOperation().ExecuteAsync(
            Context(json),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("bare tool name", result.Error);
    }

    [Fact]
    public async Task Fails_cleanly_when_tool_missing_from_system32()
    {
        var result = await new BuiltinToolOperation().ExecuteAsync(
            Context("""{"operation":"builtin-tool","name":"this-tool-does-not-exist-winevo"}"""),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task Missing_name_fails()
    {
        var result = await new BuiltinToolOperation().ExecuteAsync(
            Context("""{"operation":"builtin-tool"}"""),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("missing", result.Error);
    }

    private static OperationContext Context(string propertiesJson)
    {
        var root = JsonDocument.Parse(propertiesJson).RootElement.Clone();
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
