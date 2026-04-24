using System.Diagnostics;
using System.Text.Json;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations.Tests;

public class DelayOperationTests
{
    [Fact]
    public async Task Waits_for_the_requested_duration()
    {
        var op = DelayOperation.FromJson(Props("""{"seconds":0.2}"""));
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
        var op = DelayOperation.FromJson(Props("""{"seconds":30}"""));
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
    public void Missing_seconds_fails_at_parse_time()
    {
        var ex = Assert.Throws<JsonException>(() => DelayOperation.FromJson(Props("""{}""")));
        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public async Task Negative_seconds_fails_at_execute_time()
    {
        var op = DelayOperation.FromJson(Props("""{"seconds":-1}"""));
        var result = await op.ExecuteAsync(OperationTestContext.Empty(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("non-negative", result.Error);
    }

    [Fact]
    public async Task Seconds_from_templated_parameter()
    {
        var op = DelayOperation.FromJson(Props("""{"seconds":"{{params.wait}}"}"""));
        var ctx = OperationTestContext.WithParameters(new Dictionary<string, object?> { ["wait"] = "0.1" });

        var result = await op.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);
    }

    private static JsonElement Props(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
