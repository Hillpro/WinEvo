using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using WinEvo.ActionModel;
using WinEvo.Shell.Core.Services;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>
/// Right-pane VM: shows the selected action, hosts parameter inputs, and exposes
/// the <see cref="ExecuteCommand"/>. Displays step-result summary inline.
/// TODO: streaming progress events from the agent.
/// </summary>
public sealed partial class ActionDetailViewModel : ObservableObject
{
    private readonly Func<IAgentClient?> _agentAccessor;
    private readonly DispatcherQueue _dispatcher;

    public ActionDetailViewModel(
        ActionItemViewModel item,
        string? language,
        Func<IAgentClient?> agentAccessor,
        DispatcherQueue dispatcher)
    {
        _agentAccessor = agentAccessor;
        _dispatcher = dispatcher;
        Item = item;
        Language = language;

        foreach (var p in item.Manifest.Parameters)
            Parameters.Add(new ParameterInputViewModel(p, language));
    }

    public ActionItemViewModel Item { get; }
    public string? Language { get; }

    public ObservableCollection<ParameterInputViewModel> Parameters { get; } = new();

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial string? Status { get; set; }

    [ObservableProperty]
    public partial string? ResultDetail { get; set; }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ExecuteAsync(CancellationToken ct)
    {
        var agent = _agentAccessor();
        if (agent is null || !agent.IsConnected)
        {
            Status = "Agent not connected";
            return;
        }

        var missingRequired = Parameters
            .Where(p => p.Required && string.IsNullOrWhiteSpace(p.Value))
            .ToList();
        if (missingRequired.Count > 0)
        {
            Status = $"Missing required parameter(s): {string.Join(", ", missingRequired.Select(p => p.DisplayName))}";
            return;
        }

        IsRunning = true;
        Status = "Running…";
        ResultDetail = null;

        try
        {
            var manifestJson = JsonNode.Parse(ManifestToJson(Item.Manifest)) ?? new JsonObject();
            var paramDict = Parameters.ToDictionary(p => p.Id, p => p.ToJsonValue());

            var response = await agent.ExecuteAsync(manifestJson, paramDict, ct).ConfigureAwait(false);

            await RunOnUiAsync(() =>
            {
                Status = response.Success
                    ? $"Success — {response.Message}"
                    : $"Failed — {response.Message}";

                var detail = new System.Text.StringBuilder();
                foreach (var step in response.StepResults)
                {
                    var ok = step.Success ? "OK" : "FAIL";
                    detail.AppendLine($"[{ok}] {step.Operation}"
                        + (step.StepId is null ? "" : $" ({step.StepId})")
                        + (step.Message is null ? "" : $" — {step.Message}")
                        + (step.Error is null ? "" : $" — {step.Error}"));
                }
                foreach (var line in response.Log)
                    detail.AppendLine(line);
                ResultDetail = detail.Length == 0 ? null : detail.ToString().TrimEnd();
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RunOnUiAsync(() =>
            {
                Status = $"Error: {ex.Message}";
                ResultDetail = ex.ToString();
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsRunning = false).ConfigureAwait(false);
        }
    }

    private bool CanExecute() => !IsRunning;

    private Task RunOnUiAsync(Action action)
    {
        if (_dispatcher.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();
        var enqueued = _dispatcher.TryEnqueue(() =>
        {
            try { action(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        if (!enqueued)
            tcs.SetException(new InvalidOperationException("failed to enqueue UI update"));
        return tcs.Task;
    }

    /// <summary>Re-serialises the parsed manifest back to JSON for transport to the agent.</summary>
    private static string ManifestToJson(ActionManifest manifest)
    {
        // TODO: preserve the original raw manifest JSON end-to-end instead of
        // reconstructing a minimal subset from the parsed model.
        var root = new JsonObject
        {
            ["id"] = manifest.Id,
            ["version"] = manifest.Version,
            ["name"] = manifest.Name,
            ["category"] = manifest.Category,
        };

        var steps = new JsonArray();
        foreach (var step in manifest.Execution.Steps)
        {
            if (step is OperationStep op)
            {
                var parsed = JsonNode.Parse(op.Properties.GetRawText())!.AsObject();
                parsed["kind"] = "operation";
                steps.Add(parsed);
            }
            // sub-action steps are not supported yet; skip for transport.
        }
        root["execution"] = new JsonObject
        {
            ["mode"] = ExecutionModeToSchemaString(manifest.Execution.Mode),
            ["steps"] = steps,
        };

        var parameters = new JsonArray();
        foreach (var p in manifest.Parameters)
        {
            parameters.Add(new JsonObject
            {
                ["id"] = p.Id,
                ["type"] = p.Type,
                ["required"] = p.Required,
            });
        }
        root["parameters"] = parameters;

        return root.ToJsonString();
    }

    private static string ExecutionModeToSchemaString(ExecutionMode mode) => mode switch
    {
        ExecutionMode.SequentialContinueOnError => "sequential-continue-on-error",
        _ => "sequential",
    };
}
