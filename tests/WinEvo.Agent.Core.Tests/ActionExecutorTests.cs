using System.Text.Json;
using WinEvo.Actions.Abstractions;
using WinEvo.Ipc;

namespace WinEvo.Agent.Core.Tests;

/// <summary>
/// Pins how an action's run is reported to the Shell: which steps appear,
/// what counts as overall success, and the summary message. Actions and
/// operations are scripted, so nothing touches the real OS.
/// </summary>
public class ActionExecutorTests
{
    [Fact]
    public async Task All_steps_passing_reports_success_and_the_step_count()
    {
        var response = await Run(async (execution, ct) =>
        {
            await execution.RunAsync(ScriptedOperation.Succeeding("first"), ct);
            await execution.RunAsync(ScriptedOperation.Succeeding("second"), ct);
        });

        Assert.True(response.Success);
        Assert.Equal("completed 2 step(s)", response.Message);
        Assert.Equal(["first", "second"], response.StepResults.Select(s => s.Operation));
    }

    [Fact]
    public async Task Step_results_carry_the_operation_outcome()
    {
        var response = await Run(async (execution, ct) =>
        {
            await execution.TryRunAsync(ScriptedOperation.Succeeding("first"), ct);
            await execution.TryRunAsync(ScriptedOperation.Failing("second"), ct);
        });

        Assert.True(response.StepResults[0].Success);
        Assert.Equal("first ok", response.StepResults[0].Message);
        Assert.False(response.StepResults[1].Success);
        Assert.Equal("second failed", response.StepResults[1].Error);
    }

    [Fact]
    public async Task Failed_required_step_stops_the_action_and_names_the_step()
    {
        var response = await Run(async (execution, ct) =>
        {
            await execution.RunAsync(ScriptedOperation.Succeeding("first"), ct);
            await execution.RunAsync(ScriptedOperation.Failing("second"), ct);
            await execution.RunAsync(ScriptedOperation.Succeeding("third"), ct);
        });

        Assert.False(response.Success);
        Assert.Equal("failed at step 2 of 2", response.Message);
        Assert.DoesNotContain(response.StepResults, s => s.Operation == "third");
    }

    [Fact]
    public async Task Tolerated_failure_lets_later_steps_run_but_still_fails_the_execution()
    {
        var response = await Run(async (execution, ct) =>
        {
            await execution.TryRunAsync(ScriptedOperation.Failing("first"), ct);
            await execution.RunAsync(ScriptedOperation.Succeeding("second"), ct);
        });

        Assert.False(response.Success);
        Assert.Equal("failed at step 1 of 2", response.Message);
        Assert.True(response.StepResults[1].Success);
    }

    [Fact]
    public async Task Action_crash_is_reported_as_a_failure_and_keeps_the_steps_already_run()
    {
        var response = await Run(async (execution, ct) =>
        {
            await execution.RunAsync(ScriptedOperation.Succeeding("first"), ct);
            throw new InvalidOperationException("kaboom");
        });

        Assert.False(response.Success);
        Assert.Equal(nameof(InvalidOperationException), response.Error);
        Assert.Equal("stopped after 1 step(s): kaboom", response.Message);
        Assert.Single(response.StepResults);
    }

    [Fact]
    public async Task Log_lines_are_returned_in_order()
    {
        var response = await Run((execution, _) =>
        {
            execution.Log("one");
            execution.Log("two");
            return Task.CompletedTask;
        });

        Assert.Equal(["one", "two"], response.Log);
    }

    [Fact]
    public async Task Request_id_is_echoed()
    {
        var response = await ActionExecutor.ExecuteAsync(
            new ScriptedAction((_, _) => Task.CompletedTask), NoParameters, "r1", TestContext.Current.CancellationToken);

        Assert.Equal("r1", response.RequestId);
    }

    [Fact]
    public async Task Response_survives_the_trip_over_the_pipe()
    {
        var response = await Run(async (execution, ct) =>
        {
            execution.Log("starting");
            await execution.TryRunAsync(ScriptedOperation.Succeeding("first"), ct);
            await execution.TryRunAsync(ScriptedOperation.Failing("second"), ct);
        });

        var received = Assert.IsType<ExecutionResponse>(
            PipeMessageSerializer.Deserialize(PipeMessageSerializer.Serialize(response)));

        Assert.False(received.Success);
        Assert.Equal(response.Message, received.Message);
        Assert.Equal(["first", "second"], received.StepResults.Select(s => s.Operation));
        Assert.Equal("second failed", received.StepResults[1].Error);
        Assert.Equal(["starting"], received.Log);
    }

    [Fact]
    public async Task Already_cancelled_token_throws_before_the_action_starts()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var started = false;
        var action = new ScriptedAction((_, _) =>
        {
            started = true;
            return Task.CompletedTask;
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ActionExecutor.ExecuteAsync(action, NoParameters, null, cts.Token));

        Assert.False(started);
    }

    [Fact]
    public void BindParameters_prefers_the_supplied_value_over_the_default()
    {
        var bound = ActionExecutor.BindParameters(
            Descriptor(new StringParameter { Id = "name", Default = "fallback" }),
            new Dictionary<string, JsonElement> { ["name"] = Json("\"supplied\"") });

        Assert.Equal("supplied", bound["name"]);
    }

    [Fact]
    public void BindParameters_falls_back_to_the_default_when_nothing_or_null_is_supplied()
    {
        var descriptor = Descriptor(new BooleanParameter { Id = "flag", Default = true });

        var missing = ActionExecutor.BindParameters(descriptor, new Dictionary<string, JsonElement>());
        var suppliedNull = ActionExecutor.BindParameters(descriptor, new Dictionary<string, JsonElement> { ["flag"] = Json("null") });

        Assert.Equal(true, missing["flag"]);
        Assert.Equal(true, suppliedNull["flag"]);
    }

    [Fact]
    public void BindParameters_rejects_a_missing_required_parameter()
    {
        var descriptor = Descriptor(new DriveParameter { Id = "drive", Required = true });

        var thrown = Assert.Throws<InvalidOperationException>(
            () => ActionExecutor.BindParameters(descriptor, new Dictionary<string, JsonElement>()));

        Assert.Contains("'drive'", thrown.Message);
    }

    [Fact]
    public void BindParameters_ignores_values_for_undeclared_parameters()
    {
        var bound = ActionExecutor.BindParameters(
            Descriptor(),
            new Dictionary<string, JsonElement> { ["smuggled"] = Json("1") });

        Assert.Empty(bound);
    }

    // --- helpers ---

    private static readonly IReadOnlyDictionary<string, object?> NoParameters = new Dictionary<string, object?>();

    private static Task<ExecutionResponse> Run(Func<ActionExecution, CancellationToken, Task> body)
        => ActionExecutor.ExecuteAsync(
            new ScriptedAction(body), NoParameters, requestId: null, TestContext.Current.CancellationToken);

    private static ActionDescriptor Descriptor(params Parameter[] parameters) => new()
    {
        Id = "test.exec",
        Category = "test",
        Name = LocalizableText.Literal("Exec"),
        Parameters = parameters,
    };

    private static JsonElement Json(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private sealed class ScriptedAction(Func<ActionExecution, CancellationToken, Task> body) : ActionDefinition
    {
        public override ActionDescriptor Descriptor { get; } = ActionExecutorTests.Descriptor();

        public override Task ExecuteAsync(ActionExecution execution, CancellationToken cancellationToken)
            => body(execution, cancellationToken);
    }
}

internal sealed class ScriptedOperation : ActionOperation
{
    private readonly OperationResult _result;

    private ScriptedOperation(string id, OperationResult result)
    {
        Id = id;
        _result = result;
    }

    public override string Id { get; }

    public override Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
        => Task.FromResult(_result);

    public static ScriptedOperation Succeeding(string id) => new(id, OperationResult.Ok($"{id} ok"));
    public static ScriptedOperation Failing(string id) => new(id, OperationResult.Fail($"{id} failed"));
}
