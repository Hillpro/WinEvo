using System.Diagnostics;
using System.Text.Json;
using WinEvo.ActionModel;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations.Tests;

public class DelayOperationTests
{
    [Fact]
    public async Task Waits_for_the_requested_duration()
    {
        var sw = Stopwatch.StartNew();
        var result = await new DelayOperation().ExecuteAsync(
            Context("""{"operation":"delay","seconds":0.2}"""),
            TestContext.Current.CancellationToken);
        sw.Stop();

        Assert.True(result.Success, result.Error);
        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(150),
            $"expected ~200ms, got {sw.Elapsed.TotalMilliseconds:0}ms");
    }

    [Fact]
    public async Task Cancellation_returns_promptly()
    {
        using var cts = new CancellationTokenSource();
        var task = new DelayOperation().ExecuteAsync(
            Context("""{"operation":"delay","seconds":30}"""),
            cts.Token);

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
    public async Task Missing_seconds_fails_without_waiting()
    {
        var sw = Stopwatch.StartNew();
        var result = await new DelayOperation().ExecuteAsync(
            Context("""{"operation":"delay"}"""),
            TestContext.Current.CancellationToken);
        sw.Stop();

        Assert.False(result.Success);
        Assert.Contains("missing", result.Error);
        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public async Task Negative_seconds_fails()
    {
        var result = await new DelayOperation().ExecuteAsync(
            Context("""{"operation":"delay","seconds":-1}"""),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("non-negative", result.Error);
    }

    [Fact]
    public async Task Seconds_from_templated_parameter()
    {
        var result = await new DelayOperation().ExecuteAsync(
            Context(
                """{"operation":"delay","seconds":"{{params.wait}}"}""",
                parameters: new Dictionary<string, object?> { ["wait"] = "0.1" }),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);
    }

    private static OperationContext Context(
        string propertiesJson,
        IReadOnlyDictionary<string, object?>? parameters = null)
    {
        var root = JsonDocument.Parse(propertiesJson).RootElement.Clone();
        return new OperationContext
        {
            Step = new OperationStep
            {
                Operation = root.GetProperty("operation").GetString()!,
                Properties = root,
            },
            Parameters = parameters ?? new Dictionary<string, object?>(),
        };
    }
}
