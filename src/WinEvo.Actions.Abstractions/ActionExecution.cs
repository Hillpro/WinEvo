namespace WinEvo.Actions.Abstractions;

/// <summary>
/// One execution of an action: the parameter values it was invoked with, and
/// the record of every operation it ran. Actions run their operations through
/// <see cref="RunAsync"/> / <see cref="TryRunAsync"/> rather than calling them
/// directly, so each one is reported back to the Shell as a step.
/// </summary>
public sealed class ActionExecution
{
    private readonly IReadOnlyDictionary<string, object?> _parameters;
    private readonly List<ActionStepRecord> _steps = [];
    private readonly List<string> _logLines = [];

    /// <param name="parameters">Bound parameter values keyed by parameter id.</param>
    public ActionExecution(IReadOnlyDictionary<string, object?> parameters)
    {
        _parameters = parameters;
    }

    /// <summary>Operations run so far, in order, with their outcome.</summary>
    public IReadOnlyList<ActionStepRecord> Steps => _steps;

    public IReadOnlyList<string> LogLines => _logLines;

    public string GetString(string parameterId) => GetParameter<string>(parameterId);

    public bool GetBoolean(string parameterId) => GetParameter<bool>(parameterId);

    public long GetInteger(string parameterId) => GetParameter<long>(parameterId);

    /// <summary>
    /// Runs an operation the action cannot continue without. A failure is
    /// recorded and then ends the action by throwing
    /// <see cref="ActionStepFailedException"/>.
    /// </summary>
    public async Task RunAsync(ActionOperation operation, CancellationToken cancellationToken)
    {
        var result = await ExecuteAndRecordAsync(operation, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
            throw new ActionStepFailedException(operation.Id, result);
    }

    /// <summary>
    /// Runs an operation whose failure the action tolerates. The failure is
    /// still recorded, so the execution as a whole reports it.
    /// </summary>
    /// <returns>Whether the operation succeeded.</returns>
    public async Task<bool> TryRunAsync(ActionOperation operation, CancellationToken cancellationToken)
    {
        var result = await ExecuteAndRecordAsync(operation, cancellationToken).ConfigureAwait(false);
        return result.Success;
    }

    public void Log(string line) => _logLines.Add(line);

    private async Task<OperationResult> ExecuteAndRecordAsync(ActionOperation operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var context = new OperationContext { LogSink = line => Log($"[{operation.Id}] {line}") };

        OperationResult result;
        try
        {
            result = await operation.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            result = OperationResult.Fail(ex.GetType().Name, ex.Message);
        }

        _steps.Add(new ActionStepRecord(operation.Id, result));
        return result;
    }

    private T GetParameter<T>(string parameterId)
    {
        if (!_parameters.TryGetValue(parameterId, out var value))
            throw new InvalidOperationException($"parameter '{parameterId}' has no value");

        return value is T typed
            ? typed
            : throw new InvalidOperationException(
                $"parameter '{parameterId}' is {value?.GetType().Name ?? "null"}, not {typeof(T).Name}");
    }
}

/// <summary>An operation an action ran, and how it ended.</summary>
public sealed record ActionStepRecord(string OperationId, OperationResult Result);
