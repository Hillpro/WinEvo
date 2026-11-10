namespace WinEvo.Actions.Abstractions.Tests;

public class ActionExecutionTests
{
    [Fact]
    public async Task RunAsync_records_a_successful_operation()
    {
        var execution = NewExecution();

        await execution.RunAsync(ScriptedOperation.Succeeding("first"), Ct);

        var step = Assert.Single(execution.Steps);
        Assert.Equal("first", step.OperationId);
        Assert.True(step.Result.Success);
    }

    [Fact]
    public async Task RunAsync_records_the_failure_then_throws()
    {
        var execution = NewExecution();

        var thrown = await Assert.ThrowsAsync<ActionStepFailedException>(
            () => execution.RunAsync(ScriptedOperation.Failing("first"), Ct));

        Assert.Equal("first", thrown.OperationId);
        var step = Assert.Single(execution.Steps);
        Assert.False(step.Result.Success);
    }

    [Fact]
    public async Task TryRunAsync_records_the_failure_and_lets_the_action_continue()
    {
        var execution = NewExecution();

        var firstSucceeded = await execution.TryRunAsync(ScriptedOperation.Failing("first"), Ct);
        var secondSucceeded = await execution.TryRunAsync(ScriptedOperation.Succeeding("second"), Ct);

        Assert.False(firstSucceeded);
        Assert.True(secondSucceeded);
        Assert.Equal(["first", "second"], execution.Steps.Select(s => s.OperationId));
        Assert.False(execution.Steps[0].Result.Success);
    }

    [Fact]
    public async Task Operation_exception_is_recorded_as_a_failed_step()
    {
        var execution = NewExecution();

        var succeeded = await execution.TryRunAsync(
            ScriptedOperation.Throwing("first", new InvalidOperationException("kaboom")), Ct);

        Assert.False(succeeded);
        var step = Assert.Single(execution.Steps);
        Assert.Equal(nameof(InvalidOperationException), step.Result.Error);
        Assert.Equal("kaboom", step.Result.Message);
    }

    [Fact]
    public async Task Already_cancelled_token_stops_before_the_operation_runs()
    {
        var execution = NewExecution();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => execution.RunAsync(ScriptedOperation.Succeeding("first"), cts.Token));

        Assert.Empty(execution.Steps);
    }

    [Fact]
    public async Task Cancellation_during_an_operation_propagates_instead_of_becoming_a_failed_step()
    {
        var execution = NewExecution();
        using var cts = new CancellationTokenSource();
        var cancelsMidway = ScriptedOperation.Throwing("first", new OperationCanceledException(cts.Token));
        var operation = new CancellingOperation(cts, cancelsMidway);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution.RunAsync(operation, cts.Token));

        Assert.Empty(execution.Steps);
    }

    [Fact]
    public async Task Operation_log_lines_are_prefixed_with_the_operation_id()
    {
        var execution = NewExecution();

        await execution.RunAsync(ScriptedOperation.Logging("registry-set", "wrote value"), Ct);
        execution.Log("done");

        Assert.Equal(["[registry-set] wrote value", "done"], execution.LogLines);
    }

    [Fact]
    public void Parameter_getters_return_the_bound_value()
    {
        var execution = NewExecution(new() { ["drive"] = "C:", ["enabled"] = true, ["passes"] = 3L });

        Assert.Equal("C:", execution.GetString("drive"));
        Assert.True(execution.GetBoolean("enabled"));
        Assert.Equal(3L, execution.GetInteger("passes"));
    }

    [Fact]
    public void Missing_parameter_is_reported_by_id()
    {
        var execution = NewExecution();

        var thrown = Assert.Throws<InvalidOperationException>(() => execution.GetString("drive"));

        Assert.Contains("'drive'", thrown.Message);
    }

    [Fact]
    public void Parameter_of_the_wrong_type_is_reported_by_id()
    {
        var execution = NewExecution(new() { ["drive"] = 42L });

        var thrown = Assert.Throws<InvalidOperationException>(() => execution.GetString("drive"));

        Assert.Contains("'drive'", thrown.Message);
        Assert.Contains("String", thrown.Message);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ActionExecution NewExecution(Dictionary<string, object?>? parameters = null) => new(parameters ?? []);

    /// <summary>Cancels the caller's token, then runs the wrapped operation.</summary>
    private sealed class CancellingOperation(CancellationTokenSource cts, ActionOperation inner) : ActionOperation
    {
        public override string Id => inner.Id;

        public override async Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
        {
            await cts.CancelAsync();
            return await inner.ExecuteAsync(context, cancellationToken);
        }
    }
}
