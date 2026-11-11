using System.Text.Json.Nodes;
using WinEvo.Ipc;

namespace WinEvo.Shell.Core.Services;

/// <summary>
/// The executions sent to the agent and not answered yet, for the whole
/// session. It outlives the view model of the selected action, so an
/// execution stays listed when the user selects another action, and the
/// footer count and the close confirmation see every execution, not only the
/// selected one. An execution waiting for the pipe behind another one is
/// listed too: for the user, it has started.
/// </summary>
public sealed class ExecutionTracker
{
    private readonly Lock _gate = new();
    private readonly List<RunningExecution> _running = [];

    /// <summary>Raised, on whichever thread made the change, when an execution starts or ends.</summary>
    public event Action? Changed;

    public int RunningCount
    {
        get
        {
            lock (_gate)
                return _running.Count;
        }
    }

    /// <summary>The action id of every running execution, in the order they started.</summary>
    public IReadOnlyList<string> RunningActionIds
    {
        get
        {
            lock (_gate)
                return _running.Select(execution => execution.ActionId).ToList();
        }
    }

    /// <summary>
    /// The pending response of the latest running execution of
    /// <paramref name="actionId"/>, or <see langword="null"/> when that action
    /// is not running.
    /// </summary>
    public Task<ExecutionResponse>? FindRunning(string actionId)
    {
        lock (_gate)
            return _running.LastOrDefault(execution => execution.ActionId == actionId)?.Response;
    }

    /// <summary>
    /// Runs the action in the agent through <paramref name="client"/> and
    /// keeps it listed until its response arrives or the request fails.
    /// </summary>
    public async Task<ExecutionResponse> RunAsync(
        IAgentClient client,
        string actionId,
        IReadOnlyDictionary<string, JsonNode?> parameters,
        CancellationToken ct)
    {
        var execution = new RunningExecution(actionId, client.ExecuteAsync(actionId, parameters, ct));
        Add(execution);
        try
        {
            return await execution.Response.ConfigureAwait(false);
        }
        finally
        {
            Remove(execution);
        }
    }

    private void Add(RunningExecution execution)
    {
        lock (_gate)
            _running.Add(execution);
        Changed?.Invoke();
    }

    private void Remove(RunningExecution execution)
    {
        lock (_gate)
            _running.Remove(execution);
        Changed?.Invoke();
    }

    /// <summary>A class, not a record: two executions of the same action must stay distinct entries.</summary>
    private sealed class RunningExecution(string actionId, Task<ExecutionResponse> response)
    {
        public string ActionId { get; } = actionId;
        public Task<ExecutionResponse> Response { get; } = response;
    }
}
