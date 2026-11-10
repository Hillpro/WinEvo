using System.Diagnostics;

namespace WinEvo.Actions.Operations.Tests;

public class ProcessOperationsTests
{
    [Fact]
    public async Task PowerShell_writes_output_and_succeeds()
    {
        var op = new PowerShellOperation { Script = "Write-Output hello" };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error ?? result.Message);
        Assert.Contains("exit 0", result.Message);
    }

    [Fact]
    public async Task PowerShell_nonzero_exit_fails()
    {
        var op = new PowerShellOperation { Script = "exit 7" };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("exit 7", result.Error);
    }

    [Fact]
    public async Task PowerShell_honors_timeout()
    {
        var op = new PowerShellOperation { Script = "Start-Sleep -Seconds 30", TimeoutSeconds = 1 };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("timed out", result.Error);
    }

    [Fact]
    public async Task Command_echoes_and_succeeds()
    {
        var op = new CommandOperation { Command = "echo hello" };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error ?? result.Message);
    }

    [Fact]
    public async Task Command_nonzero_exit_fails()
    {
        var op = new CommandOperation { Command = "exit /B 3" };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("exit 3", result.Error);
    }

    [Fact]
    public async Task ExternalProcess_expands_environment_variables_in_the_path()
    {
        var op = new ExternalProcessOperation { Path = @"%SystemRoot%\System32\whoami.exe" };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error ?? result.Message);
    }

    [Fact]
    public async Task ExternalProcess_passes_an_argument_containing_a_space_as_one_argument()
    {
        // findstr exits 0 only if it can open the file and finds the text. If
        // the path were split at its space, it would look for two files that
        // do not exist.
        var directory = Directory.CreateTempSubdirectory("WinEvo Tests ");
        try
        {
            var fileWithSpace = Path.Combine(directory.FullName, "two words.txt");
            await File.WriteAllTextAsync(fileWithSpace, "needle", TestContext.Current.CancellationToken);

            var op = new ExternalProcessOperation
            {
                Path = @"%SystemRoot%\System32\findstr.exe",
                Args = ["/M", "needle", fileWithSpace],
            };
            var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.Error ?? result.Message);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ExternalProcess_cancellation_returns_before_child_finishes()
    {
        // cmd spawns a 30 s ping. Cancel after 200 ms; the op must return
        // promptly (well under the full sleep) because ProcessRunner kills
        // the child on the linked token firing.
        var op = new ExternalProcessOperation
        {
            Path = "cmd.exe",
            Args = ["/C", "ping 127.0.0.1 -n 30 > nul"],
        };

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
}
