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
public sealed class DelayOperation : IActionOperation
{
    public string Id => "delay";

    public async Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        if (!TryReadSeconds(context, out var seconds, out var error))
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

    private static bool TryReadSeconds(OperationContext context, out double seconds, out string error)
    {
        seconds = 0;
        if (!context.Step.Properties.TryGetProperty("seconds", out var prop))
        {
            error = "missing 'seconds' property";
            return false;
        }

        switch (prop.ValueKind)
        {
            case JsonValueKind.Number:
                seconds = prop.GetDouble();
                break;
            case JsonValueKind.String:
                var rendered = Templating.Render(prop.GetString() ?? "", context.Parameters);
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
