using System.Text.Json;
using WinEvo.ActionModel;
using WinEvo.Actions.Abstractions;
using WinEvo.Ipc;

namespace WinEvo.Agent.Core.Tests;

/// <summary>
/// Pins the executor's decision logic — continue-on-error precedence,
/// break-vs-continue on parse-vs-execute failure, the overall-success and
/// failed-step-index messages, and sub-action stubbing — using a stub
/// <see cref="IOperationParser"/> so nothing touches the real OS.
/// </summary>
public class ActionExecutorTests
{
    [Fact]
    public async Task Sequential_stops_at_first_failure()
    {
        var response = await Run(ExecutionMode.Sequential, Op("ok-1"), Op("fail-2"), Op("ok-3"));

        Assert.False(response.Success);
        Assert.Equal(2, response.StepResults.Count); // ok-3 never ran
        Assert.DoesNotContain(response.StepResults, s => s.StepId == "ok-3");
    }

    [Fact]
    public async Task ContinueOnError_mode_runs_every_step()
    {
        var response = await Run(ExecutionMode.SequentialContinueOnError, Op("ok-1"), Op("fail-2"), Op("ok-3"));

        Assert.False(response.Success);             // a step failed
        Assert.Equal(3, response.StepResults.Count); // but all ran
    }

    [Fact]
    public async Task PerStep_ContinueOnError_overrides_Sequential()
    {
        var response = await Run(ExecutionMode.Sequential, Op("fail-1", continueOnError: true), Op("ok-2"));

        Assert.False(response.Success);
        Assert.Equal(2, response.StepResults.Count); // ok-2 ran despite the earlier failure
        Assert.True(response.StepResults[1].Success);
    }

    [Fact]
    public async Task Parser_throw_is_recorded_as_a_failed_step_not_rethrown()
    {
        var response = await Run(ExecutionMode.Sequential, Op("throwparse"));

        Assert.False(response.Success);
        var step = Assert.Single(response.StepResults);
        Assert.False(step.Success);
        Assert.Contains("failed to parse operation", step.Error);
    }

    [Fact]
    public async Task Operation_throw_is_caught_and_reported_as_failure()
    {
        var response = await Run(ExecutionMode.Sequential, Op("boom"));

        var step = Assert.Single(response.StepResults);
        Assert.False(step.Success);
        Assert.Equal(nameof(InvalidOperationException), step.Error);
    }

    [Fact]
    public async Task Failed_step_index_is_one_based()
    {
        var response = await Run(ExecutionMode.Sequential, Op("ok-1"), Op("ok-2"), Op("fail-3"));

        Assert.False(response.Success);
        Assert.Contains("failed at step 3 of 3", response.Message);
    }

    [Fact]
    public async Task All_pass_reports_step_count()
    {
        var response = await Run(ExecutionMode.Sequential, Op("ok-1"), Op("ok-2"));

        Assert.True(response.Success);
        Assert.Contains("completed 2 step(s)", response.Message);
    }

    [Fact]
    public async Task Sub_action_step_is_reported_unsupported()
    {
        var response = await Run(ExecutionMode.Sequential,
            new SubActionStep { Id = "s1", Ref = "other.action", MinVersion = "1.0.0" });

        Assert.False(response.Success);
        var step = Assert.Single(response.StepResults);
        Assert.Equal("sub-action steps are not supported yet", step.Error);
        Assert.Equal("sub-action:other.action", step.Operation);
    }

    [Fact]
    public async Task Already_cancelled_token_throws_before_running_steps()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ActionExecutor(Parser()).ExecuteAsync(
                Manifest(ExecutionMode.Sequential, Op("ok-1")), Empty, null, cts.Token));
    }

    // --- helpers ---

    private static readonly IReadOnlyDictionary<string, object?> Empty = new Dictionary<string, object?>();

    private static Task<ExecutionResponse> Run(ExecutionMode mode, params ActionStep[] steps)
        => new ActionExecutor(Parser()).ExecuteAsync(
            Manifest(mode, steps), Empty, requestId: null, TestContext.Current.CancellationToken);

    private static OperationStep Op(string id, bool continueOnError = false) => new()
    {
        Id = id,
        Operation = id,
        ContinueOnError = continueOnError,
        RawProperties = JsonDocument.Parse("{}").RootElement.Clone(),
    };

    private static ActionManifest Manifest(ExecutionMode mode, params ActionStep[] steps) => new()
    {
        Id = "test.exec",
        Version = "1.0.0",
        Name = "Exec",
        Category = "storage",
        RawJson = "{}",
        Execution = new Execution { Mode = mode, Steps = steps },
    };

    // Convention-based stub: operation id prefix decides the outcome.
    private static StubParser Parser() => new(id => id switch
    {
        "throwparse" => throw new JsonException($"unparseable operation '{id}'"),
        "boom" => ScriptedOperation.Throws(id),
        _ when id.StartsWith("fail", StringComparison.Ordinal) => ScriptedOperation.Failing(id),
        _ => ScriptedOperation.Succeeding(id),
    });
}

internal sealed class StubParser(Func<string, ActionOperation> factory) : IOperationParser
{
    public ActionOperation Parse(string operationId, JsonElement properties) => factory(operationId);
    public IReadOnlyCollection<string> SupportedIds => [];
}

internal sealed class ScriptedOperation : ActionOperation
{
    private readonly string _id;
    private readonly OperationResult? _result;
    private readonly Exception? _throw;

    private ScriptedOperation(string id, OperationResult? result, Exception? toThrow)
    {
        _id = id;
        _result = result;
        _throw = toThrow;
    }

    public override string Id => _id;

    public override Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
        => _throw is not null ? throw _throw : Task.FromResult(_result!);

    public static ScriptedOperation Succeeding(string id) => new(id, OperationResult.Ok($"{id} ok"), null);
    public static ScriptedOperation Failing(string id) => new(id, OperationResult.Fail($"{id} failed"), null);
    public static ScriptedOperation Throws(string id) => new(id, null, new InvalidOperationException("kaboom"));
}
