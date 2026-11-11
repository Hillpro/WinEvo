using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations.Tests;

public class ProcessKillOperationTests
{
    [Fact]
    public async Task Nothing_to_kill_succeeds_and_names_the_process_without_a_log_line()
    {
        var logLines = new List<string>();
        var context = new OperationContext { LogSink = logLines.Add };
        var name = $"WinEvo.Tests.NotRunning.{Guid.NewGuid():N}";

        var result = await new ProcessKillOperation { Name = name }.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal($"killed 0 process(es) named '{name}'", result.Message);
        Assert.Empty(logLines);
    }
}
