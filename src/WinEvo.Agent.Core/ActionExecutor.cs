using System.Text.Json;
using WinEvo.Actions.Abstractions;
using WinEvo.Ipc;

namespace WinEvo.Agent.Core;

/// <summary>
/// Runs an <see cref="ActionDefinition"/> and reports the outcome in the IPC
/// shape. The action decides which operations run and whether a failed one
/// stops it; this class only starts it, contains its exceptions and turns the
/// recorded <see cref="ActionExecution"/> into an <see cref="ExecutionResponse"/>.
/// </summary>
public static class ActionExecutor
{
    public static async Task<ExecutionResponse> ExecuteAsync(
        ActionDefinition action,
        IReadOnlyDictionary<string, object?> parameters,
        string? requestId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var execution = new ActionExecution(parameters);
        Exception? crash = null;
        try
        {
            await action.ExecuteAsync(execution, ct).ConfigureAwait(false);
        }
        catch (ActionStepFailedException)
        {
            // The action stopped on a failed operation, which is already
            // recorded as a step.
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            crash = ex;
        }

        var stepResults = execution.Steps
            .Select(step => new StepResult
            {
                Operation = step.OperationId,
                Success = step.Result.Success,
                Message = step.Result.Message,
                Error = step.Result.Error,
                FailureTolerated = step.FailureTolerated,
            })
            .ToList();

        return new ExecutionResponse
        {
            RequestId = requestId,
            Success = crash is null && stepResults.All(s => s.Success || s.FailureTolerated),
            Message = Summarize(stepResults, crash),
            Error = crash is null ? null : crash.GetType().Name,
            StepResults = stepResults,
            Log = execution.LogLines,
        };
    }

    private static string Summarize(List<StepResult> stepResults, Exception? crash)
    {
        if (crash is not null)
            return $"stopped after {stepResults.Count} step(s): {crash.Message}";

        var stoppingFailure = stepResults.FindIndex(s => !s.Success && !s.FailureTolerated);
        if (stoppingFailure >= 0)
            return $"failed at step {stoppingFailure + 1} of {stepResults.Count}";

        var toleratedFailures = stepResults.Count(s => s.FailureTolerated);
        return toleratedFailures == 0
            ? $"completed {stepResults.Count} step(s)"
            : $"completed {stepResults.Count} step(s), {toleratedFailures} failed without stopping the action";
    }

    /// <summary>
    /// Resolves the value of every declared parameter from what the caller
    /// sent over IPC: the supplied value, else the declared default. A
    /// required parameter with neither is an error.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> BindParameters(
        ActionDescriptor descriptor,
        IReadOnlyDictionary<string, JsonElement> raw)
    {
        var result = new Dictionary<string, object?>();

        foreach (var p in descriptor.Parameters)
        {
            if (raw.TryGetValue(p.Id, out var supplied) && supplied.ValueKind != JsonValueKind.Null)
            {
                result[p.Id] = ConvertToClr(supplied);
            }
            else if (p.DefaultValue is { } declaredDefault)
            {
                result[p.Id] = declaredDefault;
            }
            else if (p.Required)
            {
                throw new InvalidOperationException($"required parameter '{p.Id}' not supplied");
            }
        }

        return result;
    }

    private static object? ConvertToClr(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => el.GetRawText(),
    };
}
