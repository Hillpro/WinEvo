using System.Globalization;
using System.Text.Json;
using WinEvo.ActionModel;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Waits for the configured duration before completing. Manifest shape:
/// <code>
/// { "operation": "delay", "seconds": 2 }
/// </code>
/// Cooperatively cancellable via the outer token. Useful between steps that
/// need a settle delay (e.g. netsh disconnect/connect sequences).
/// </summary>
public sealed class DelayOperation : ActionOperation
{
    public override string Id => "delay";

    /// <summary>Either a literal number of seconds, or a template string that renders to one (e.g. <c>"{{params.wait}}"</c>).</summary>
    public required JsonElement Seconds { get; init; }

    public static DelayOperation FromJson(JsonElement properties)
    {
        if (!properties.TryGetProperty("seconds", out var seconds))
            throw new JsonException("delay: missing 'seconds' property");
        return new DelayOperation { Seconds = seconds.Clone() };
    }

    public override async Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        if (!TryResolveSeconds(context, out var seconds, out var error))
            return OperationResult.Fail(error);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken).ConfigureAwait(false);
            context.Log($"waited {seconds}s");
            return OperationResult.Ok($"waited {seconds}s");
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Fail("cancelled");
        }
    }

    private bool TryResolveSeconds(OperationContext context, out double seconds, out string error)
    {
        seconds = 0;
        switch (Seconds.ValueKind)
        {
            case JsonValueKind.Number:
                seconds = Seconds.GetDouble();
                break;
            case JsonValueKind.String:
                var rendered = RenderProperty(Seconds.GetString() ?? "", context);
                if (!double.TryParse(rendered, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
                {
                    error = $"'seconds' is not a number: '{rendered}'";
                    return false;
                }
                break;
            default:
                error = "'seconds' must be a number";
                return false;
        }

        if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds))
        {
            error = $"'seconds' must be non-negative and finite, got {seconds}";
            return false;
        }

        error = "";
        return true;
    }
}
