using System.Diagnostics;
using System.Text.Json;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Kills a process by name or PID. Manifest shape:
/// <code>
/// { "operation": "process-kill", "name": "SearchUI" }
/// { "operation": "process-kill", "pid": 1234 }
/// </code>
/// </summary>
public sealed class ProcessKillOperation : IActionOperation
{
    public string Id => "process-kill";

    // System processes require elevation; user processes don't. Conservative default: false.
    public bool RequiresElevation => false;

    public Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        var props = context.Step.Properties;
        var killed = 0;

        try
        {
            if (props.TryGetProperty("pid", out var pidElement) && pidElement.ValueKind == JsonValueKind.Number)
            {
                var pid = pidElement.GetInt32();
                using var process = Process.GetProcessById(pid);
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5_000);
                killed = 1;
                context.Log($"killed pid {pid}");
            }
            else
            {
                var name = context.RenderProperty("name");
                if (string.IsNullOrWhiteSpace(name))
                    return Task.FromResult(OperationResult.Fail("neither 'name' nor 'pid' provided"));

                var processes = Process.GetProcessesByName(name);
                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(5_000);
                        killed++;
                    }
                    catch (Exception ex)
                    {
                        context.Log($"failed to kill '{name}' (pid {process.Id}): {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
                context.Log($"killed {killed} process(es) named '{name}'");
            }

            return Task.FromResult(OperationResult.Ok($"killed {killed} process(es)"));
        }
        catch (ArgumentException)
        {
            // GetProcessById throws when the pid is not running; treat as success (nothing to kill).
            context.Log("target process not running");
            return Task.FromResult(OperationResult.Ok("process not running"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult.Fail(ex.GetType().Name, ex.Message));
        }
    }
}
