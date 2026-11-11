using System.Text.Json.Nodes;
using NSubstitute;
using WinEvo.Ipc;
using WinEvo.Shell.Core.Services;

namespace WinEvo.Shell.Core.Tests;

public class ExecutionTrackerTests
{
    private const string LongAction = "test.long-action";
    private const string OtherAction = "test.other-action";

    private static readonly IReadOnlyDictionary<string, JsonNode?> s_noParameters = new Dictionary<string, JsonNode?>();

    private readonly ExecutionTracker _tracker = new();
    private readonly IAgentClient _client = Substitute.For<IAgentClient>();

    [Fact]
    public async Task An_execution_is_listed_until_its_response_arrives()
    {
        var response = PendingResponseFor(LongAction);

        var running = Run(LongAction);

        Assert.Equal(1, _tracker.RunningCount);
        Assert.Equal(new[] { LongAction }, _tracker.RunningActionIds);
        Assert.NotNull(_tracker.FindRunning(LongAction));

        response.SetResult(Succeeded());
        await running;

        Assert.Equal(0, _tracker.RunningCount);
        Assert.Empty(_tracker.RunningActionIds);
        Assert.Null(_tracker.FindRunning(LongAction));
    }

    [Fact]
    public async Task Executions_started_while_another_runs_are_all_counted()
    {
        var first = PendingResponseFor(LongAction);
        var second = PendingResponseFor(OtherAction);

        var firstRunning = Run(LongAction);
        var secondRunning = Run(OtherAction);

        Assert.Equal(2, _tracker.RunningCount);
        Assert.Equal(new[] { LongAction, OtherAction }, _tracker.RunningActionIds);

        first.SetResult(Succeeded());
        await firstRunning;

        Assert.Equal(new[] { OtherAction }, _tracker.RunningActionIds);

        second.SetResult(Succeeded());
        await secondRunning;

        Assert.Equal(0, _tracker.RunningCount);
    }

    [Fact]
    public async Task Changed_is_raised_when_an_execution_starts_and_when_it_ends()
    {
        var countsSeen = new List<int>();
        _tracker.Changed += () => countsSeen.Add(_tracker.RunningCount);
        var response = PendingResponseFor(LongAction);

        var running = Run(LongAction);
        response.SetResult(Succeeded());
        await running;

        Assert.Collection(countsSeen,
            countAtStart => Assert.Equal(1, countAtStart),
            countAtEnd => Assert.Equal(0, countAtEnd));
    }

    [Fact]
    public async Task An_execution_the_agent_reports_as_failed_is_no_longer_listed()
    {
        var response = PendingResponseFor(LongAction);
        var failed = new ExecutionResponse { Success = false, Error = "step 1 failed" };

        var running = Run(LongAction);
        response.SetResult(failed);

        Assert.Same(failed, await running);
        Assert.Equal(0, _tracker.RunningCount);
    }

    [Fact]
    public async Task An_execution_whose_request_throws_is_no_longer_listed_and_the_caller_gets_the_error()
    {
        var response = PendingResponseFor(LongAction);
        var changes = 0;
        _tracker.Changed += () => changes++;

        var running = Run(LongAction);
        response.SetException(new InvalidOperationException("agent closed pipe"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => running);
        Assert.Equal("agent closed pipe", error.Message);
        Assert.Equal(0, _tracker.RunningCount);
        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task A_cancelled_execution_is_no_longer_listed()
    {
        var response = PendingResponseFor(LongAction);

        var running = Run(LongAction);
        response.SetCanceled(TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.Equal(0, _tracker.RunningCount);
    }

    [Fact]
    public async Task FindRunning_hands_out_the_response_the_execution_receives()
    {
        var response = PendingResponseFor(LongAction);
        var running = Run(LongAction);

        var followed = _tracker.FindRunning(LongAction);

        Assert.NotNull(followed);
        Assert.False(followed.IsCompleted);
        Assert.Null(_tracker.FindRunning(OtherAction));

        response.SetResult(Succeeded());
        Assert.Same(await running, await followed);
    }

    [Fact]
    public async Task Two_executions_of_the_same_action_are_listed_separately()
    {
        var first = new TaskCompletionSource<ExecutionResponse>();
        var second = new TaskCompletionSource<ExecutionResponse>();
        _client.ExecuteAsync(LongAction, Arg.Any<IReadOnlyDictionary<string, JsonNode?>>(), Arg.Any<CancellationToken>())
            .Returns(first.Task, second.Task);

        var firstRunning = Run(LongAction);
        var secondRunning = Run(LongAction);

        Assert.Equal(2, _tracker.RunningCount);

        first.SetResult(Succeeded());
        await firstRunning;

        Assert.Equal(1, _tracker.RunningCount);
        Assert.Same(second.Task, _tracker.FindRunning(LongAction));

        second.SetResult(Succeeded());
        await secondRunning;

        Assert.Equal(0, _tracker.RunningCount);
    }

    private TaskCompletionSource<ExecutionResponse> PendingResponseFor(string actionId)
    {
        var response = new TaskCompletionSource<ExecutionResponse>();
        _client.ExecuteAsync(actionId, Arg.Any<IReadOnlyDictionary<string, JsonNode?>>(), Arg.Any<CancellationToken>())
            .Returns(response.Task);
        return response;
    }

    private Task<ExecutionResponse> Run(string actionId)
        => _tracker.RunAsync(_client, actionId, s_noParameters, TestContext.Current.CancellationToken);

    private static ExecutionResponse Succeeded() => new() { Success = true };
}
