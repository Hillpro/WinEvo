using System.Diagnostics;

namespace WinEvo.Actions.Operations.Tests;

public class DelayOperationTests
{
    [Fact]
    public async Task Waits_for_the_requested_duration()
    {
        var op = new DelayOperation { Duration = TimeSpan.FromMilliseconds(200) };
        var sw = Stopwatch.StartNew();
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);
        sw.Stop();

        Assert.True(result.Success, result.Error);
        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(150),
            $"expected ~200ms, got {sw.Elapsed.TotalMilliseconds:0}ms");
    }

    [Fact]
    public async Task Cancellation_returns_promptly()
    {
        var op = new DelayOperation { Duration = TimeSpan.FromSeconds(30) };
        using var cts = new CancellationTokenSource();
        var task = op.ExecuteAsync(OperationTestContext.Empty(), cts.Token);

        cts.CancelAfter(TimeSpan.FromMilliseconds(50));
        var sw = Stopwatch.StartNew();
        var result = await task;
        sw.Stop();

        Assert.False(result.Success);
        Assert.Equal("cancelled", result.Error);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2),
            $"cancellation was not prompt: {sw.Elapsed.TotalSeconds:0.0}s");
    }

    [Fact]
    public async Task Negative_duration_fails()
    {
        var op = new DelayOperation { Duration = TimeSpan.FromSeconds(-1) };
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("non-negative", result.Error);
    }
}
