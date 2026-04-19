using System.Text.Json;
using WinEvo.ActionModel;
using WinEvo.Actions.Abstractions;
using WinEvo.Actions.Operations;
using WinEvo.Ipc;

namespace WinEvo.Agent.Core;

/// <summary>
/// Runs an <see cref="ActionManifest"/> step-by-step using the registered
/// operations. TODO: sub-action steps are not yet expanded; they are reported
/// as a skipped/failed step result.
/// </summary>
public sealed class ActionExecutor
{
    private readonly OperationCatalog _operations;

    public ActionExecutor(OperationCatalog operations)
    {
        _operations = operations;
    }

    public async Task<ExecutionResponse> ExecuteAsync(
        ActionManifest manifest,
        IReadOnlyDictionary<string, object?> parameters,
        string? requestId,
        CancellationToken ct)
    {
        var stepResults = new List<StepResult>();
        var log = new List<string>();
        bool continueOnErrorMode = manifest.Execution.Mode == ExecutionMode.SequentialContinueOnError;

        foreach (var step in manifest.Execution.Steps)
        {
            ct.ThrowIfCancellationRequested();

            if (step is SubActionStep sub)
            {
                // TODO: resolve sub-action, bind parameters, recurse.
                stepResults.Add(new StepResult
                {
                    StepId = step.Id,
                    Operation = $"sub-action:{sub.Ref}",
                    Success = false,
                    Error = "sub-action steps are not supported yet",
                });
                if (!continueOnErrorMode && !step.ContinueOnError)
                    break;
                continue;
            }

            if (step is not OperationStep op)
                continue;

            if (!_operations.TryGet(op.Operation, out var impl))
            {
                stepResults.Add(new StepResult
                {
                    StepId = step.Id,
                    Operation = op.Operation,
                    Success = false,
                    Error = $"operation '{op.Operation}' is not implemented",
                });
                if (!continueOnErrorMode && !step.ContinueOnError)
                    break;
                continue;
            }

            var context = new OperationContext
            {
                Step = op,
                Parameters = parameters,
                LogSink = line => log.Add($"[{step.Id ?? op.Operation}] {line}"),
            };

            OperationResult result;
            try
            {
                result = await impl.ExecuteAsync(context, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                result = OperationResult.Fail(ex.GetType().Name, ex.Message);
            }

            stepResults.Add(new StepResult
            {
                StepId = step.Id,
                Operation = op.Operation,
                Success = result.Success,
                Message = result.Message,
                Error = result.Error,
            });

            if (!result.Success && !continueOnErrorMode && !step.ContinueOnError)
                break;
        }

        var overall = stepResults.All(s => s.Success);
        return new ExecutionResponse
        {
            RequestId = requestId,
            Success = overall,
            Message = overall
                ? $"completed {stepResults.Count} step(s)"
                : $"failed at step {stepResults.FindIndex(s => !s.Success) + 1} of {stepResults.Count}",
            StepResults = stepResults,
            Log = log,
        };
    }

    /// <summary>Convert a JSON-node parameter map (as received over IPC) to a plain object dictionary for templating.</summary>
    public static IReadOnlyDictionary<string, object?> BindParameters(
        ActionManifest manifest,
        IReadOnlyDictionary<string, JsonElement> raw)
    {
        var result = new Dictionary<string, object?>();

        foreach (var p in manifest.Parameters)
        {
            if (raw.TryGetValue(p.Id, out var supplied) && supplied.ValueKind != JsonValueKind.Null)
            {
                result[p.Id] = ConvertToClr(supplied);
            }
            else if (p.Default is { } def && def.ValueKind != JsonValueKind.Null)
            {
                result[p.Id] = ConvertToClr(def);
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
