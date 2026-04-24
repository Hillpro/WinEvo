namespace WinEvo.Actions.Abstractions;

/// <summary>
/// Per-invocation execution context. Holds the resolved parameter values and
/// an optional log sink.
/// </summary>
public sealed class OperationContext
{
    /// <summary>
    /// Parameters bound for this action invocation. Keys match the parameter
    /// <c>id</c> declared in the manifest; values are the CLR representation
    /// produced by <c>ActionExecutor.BindParameters</c>.
    /// </summary>
    public required IReadOnlyDictionary<string, object?> Parameters { get; init; }

    /// <summary>Optional sink for streaming log lines back to the caller.</summary>
    public Action<string>? LogSink { get; init; }

    /// <summary>Writes a single log line to <see cref="LogSink"/> if one is attached.</summary>
    public void Log(string message) => LogSink?.Invoke(message);
}
