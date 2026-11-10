namespace WinEvo.Actions.Operations.Tests;

public class BuiltinToolOperationTests
{
    [Fact]
    public async Task Runs_a_known_system32_tool_with_explicit_exe()
    {
        // whoami.exe is present on every supported Windows build.
        var op = new BuiltinToolOperation { Name = "whoami.exe" };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error ?? result.Message);
    }

    [Fact]
    public async Task Accepts_tool_name_without_exe_suffix()
    {
        var op = new BuiltinToolOperation { Name = "whoami" };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error ?? result.Message);
    }

    [Fact]
    public async Task Passes_each_argument_to_the_tool()
    {
        // "whoami /?" exits 0; an unknown switch would exit non-zero.
        var op = new BuiltinToolOperation { Name = "whoami", Args = ["/?"] };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error ?? result.Message);
    }

    [Theory]
    [InlineData(@"C:\Windows\System32\whoami.exe")]
    [InlineData(@"..\..\Program Files\evil.exe")]
    [InlineData(@"subdir\whoami.exe")]
    public async Task Rejects_non_bare_names(string name)
    {
        var op = new BuiltinToolOperation { Name = name };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("bare tool name", result.Error);
    }

    [Fact]
    public async Task Fails_cleanly_when_tool_missing_from_system32()
    {
        var op = new BuiltinToolOperation { Name = "this-tool-does-not-exist-winevo" };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }
}
