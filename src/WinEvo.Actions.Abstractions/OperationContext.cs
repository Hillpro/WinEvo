namespace WinEvo.Actions.Abstractions;

/// <summary>What an operation is given while it runs: a place to log.</summary>
public sealed class OperationContext
{
    /// <summary>Optional sink for streaming log lines back to the caller.</summary>
    public Action<string>? LogSink { get; init; }

    /// <summary>Writes a single log line to <see cref="LogSink"/> if one is attached.</summary>
    public void Log(string message) => LogSink?.Invoke(message);
}
