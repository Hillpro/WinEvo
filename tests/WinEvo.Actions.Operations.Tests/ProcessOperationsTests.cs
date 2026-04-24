using System.Diagnostics;
using System.Text.Json;

namespace WinEvo.Actions.Operations.Tests;

public class ProcessOperationsTests
{
    [Fact]
    public async Task PowerShell_writes_output_and_succeeds()
    {
        var op = PowerShellOperation.FromJson(Props("""{"script":"Write-Output hello"}"""));
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error ?? result.Message);
        Assert.Contains("exit 0", result.Message);
    }

    [Fact]
    public async Task PowerShell_nonzero_exit_fails()
    {
        var op = PowerShellOperation.FromJson(Props("""{"script":"exit 7"}"""));
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("exit 7", result.Error);
    }

    [Fact]
    public void PowerShell_missing_script_fails_at_parse_time()
    {
        var ex = Assert.Throws<JsonException>(() => PowerShellOperation.FromJson(Props("""{}""")));
        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public async Task PowerShell_renders_template_parameters_before_dispatch()
    {
        var op = PowerShellOperation.FromJson(Props(
            """{"script":"if ('{{params.msg}}' -ne 'templated') { exit 1 }"}"""));
        var ctx = OperationTestContext.WithParameters(new Dictionary<string, object?> { ["msg"] = "templated" });

        var result = await op.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error ?? result.Message);
    }

    [Fact]
    public async Task PowerShell_honors_timeout()
    {
        var op = PowerShellOperation.FromJson(Props("""{"script":"Start-Sleep -Seconds 30","timeout":1}"""));
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("timed out", result.Error);
    }

    [Fact]
    public async Task Command_echoes_and_succeeds()
    {
        var op = CommandOperation.FromJson(Props("""{"command":"echo hello"}"""));
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error ?? result.Message);
    }

    [Fact]
    public async Task Command_nonzero_exit_fails()
    {
        var op = CommandOperation.FromJson(Props("""{"command":"exit /B 3"}"""));
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("exit 3", result.Error);
    }

    [Fact]
    public void Command_missing_command_fails_at_parse_time()
    {
        var ex = Assert.Throws<JsonException>(() => CommandOperation.FromJson(Props("""{}""")));
        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public async Task ExternalProcess_cancellation_returns_before_child_finishes()
    {
        // cmd spawns a 30 s ping. Cancel after 200 ms; the op must return
        // promptly (well under the full sleep) because ProcessRunner kills
        // the child on the linked token firing.
        var op = ExternalProcessOperation.FromJson(Props(
            """{"path":"cmd.exe","args":["/C","ping 127.0.0.1 -n 30 > nul"]}"""));

        using var cts = new CancellationTokenSource();
        var stopwatch = Stopwatch.StartNew();
        var task = op.ExecuteAsync(OperationTestContext.Empty(), cts.Token);
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));
        var result = await task;
        stopwatch.Stop();

        Assert.False(result.Success);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"expected prompt cancellation, actually took {stopwatch.Elapsed.TotalSeconds:0.0}s");
    }

    private static JsonElement Props(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
