using System.Diagnostics;
using System.Text.Json;
using WinEvo.ActionModel;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations.Tests;

public class ProcessOperationsTests
{
    [Fact]
    public async Task PowerShell_writes_output_and_succeeds()
    {
        var result = await new PowerShellOperation().ExecuteAsync(
            Context("""{"operation":"powershell","script":"Write-Output hello"}"""),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error ?? result.Message);
        Assert.Contains("exit 0", result.Message);
    }

    [Fact]
    public async Task PowerShell_nonzero_exit_fails()
    {
        var result = await new PowerShellOperation().ExecuteAsync(
            Context("""{"operation":"powershell","script":"exit 7"}"""),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("exit 7", result.Error);
    }

    [Fact]
    public async Task PowerShell_missing_script_fails_without_spawning()
    {
        var result = await new PowerShellOperation().ExecuteAsync(
            Context("""{"operation":"powershell"}"""),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("missing", result.Error);
    }

    [Fact]
    public async Task PowerShell_renders_template_parameters_before_dispatch()
    {
        var result = await new PowerShellOperation().ExecuteAsync(
            Context(
                """{"operation":"powershell","script":"if ('{{params.msg}}' -ne 'templated') { exit 1 }"}""",
                parameters: new Dictionary<string, object?> { ["msg"] = "templated" }),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error ?? result.Message);
    }

    [Fact]
    public async Task PowerShell_honors_timeout()
    {
        var result = await new PowerShellOperation().ExecuteAsync(
            Context("""{"operation":"powershell","script":"Start-Sleep -Seconds 30","timeout":1}"""),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("timed out", result.Error);
    }

    [Fact]
    public async Task Command_echoes_and_succeeds()
    {
        var result = await new CommandOperation().ExecuteAsync(
            Context("""{"operation":"command","command":"echo hello"}"""),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error ?? result.Message);
    }

    [Fact]
    public async Task Command_nonzero_exit_fails()
    {
        var result = await new CommandOperation().ExecuteAsync(
            Context("""{"operation":"command","command":"exit /B 3"}"""),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("exit 3", result.Error);
    }

    [Fact]
    public async Task Command_missing_command_fails_without_spawning()
    {
        var result = await new CommandOperation().ExecuteAsync(
            Context("""{"operation":"command"}"""),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("missing", result.Error);
    }

    [Fact]
    public async Task ExternalProcess_cancellation_returns_before_child_finishes()
    {
        // cmd spawns a 30 s ping. Cancel after 200 ms; the op must return
        // promptly (well under the full sleep) because ProcessRunner kills
        // the child on the linked token firing.
        var ctx = Context(
            """{"operation":"external-process","path":"cmd.exe","args":["/C","ping 127.0.0.1 -n 30 > nul"]}""");

        using var cts = new CancellationTokenSource();
        var stopwatch = Stopwatch.StartNew();
        var task = new ExternalProcessOperation().ExecuteAsync(ctx, cts.Token);
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));
        var result = await task;
        stopwatch.Stop();

        Assert.False(result.Success);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"expected prompt cancellation, actually took {stopwatch.Elapsed.TotalSeconds:0.0}s");
    }

    private static OperationContext Context(
        string propertiesJson,
        IReadOnlyDictionary<string, object?>? parameters = null,
        Action<string>? log = null)
    {
        var root = JsonDocument.Parse(propertiesJson).RootElement.Clone();
        var step = new OperationStep
        {
            Operation = root.GetProperty("operation").GetString()!,
            Properties = root,
        };
        return new OperationContext
        {
            Step = step,
            Parameters = parameters ?? new Dictionary<string, object?>(),
            LogSink = log,
        };
    }
}
