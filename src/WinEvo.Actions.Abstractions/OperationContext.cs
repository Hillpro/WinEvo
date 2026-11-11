namespace WinEvo.Actions.Abstractions;

/// <summary>What an operation is given while it runs: a place to log.</summary>
public sealed class OperationContext
{
    /// <summary>Optional sink for streaming log lines back to the caller.</summary>
    public Action<string>? LogSink { get; init; }

    /// <summary>
    /// Writes a single log line to <see cref="LogSink"/> if one is attached.
    /// Only for what the operation's result does not already say, such as the
    /// command line it started or an item it could not handle: the result and
    /// the log are shown together.
    /// </summary>
    public void Log(string message) => LogSink?.Invoke(message);
}
