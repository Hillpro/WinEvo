using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Waits before the action continues:
/// <code>
/// new DelayOperation { Duration = TimeSpan.FromSeconds(2) }
/// </code>
/// Cooperatively cancellable via the outer token. Useful between steps that
/// need a settle delay (e.g. netsh disconnect/connect sequences).
/// </summary>
public sealed class DelayOperation : ActionOperation
{
    public override string Id => "delay";

    public required TimeSpan Duration { get; init; }

    public override async Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        if (Duration < TimeSpan.Zero)
            return OperationResult.Fail($"duration must be non-negative, got {Duration.TotalSeconds}s");

        try
        {
            await Task.Delay(Duration, cancellationToken).ConfigureAwait(false);
            context.Log($"waited {Duration.TotalSeconds}s");
            return OperationResult.Ok($"waited {Duration.TotalSeconds}s");
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Fail("cancelled");
        }
    }
}
