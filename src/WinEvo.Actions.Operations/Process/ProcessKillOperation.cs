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
/// Exactly one of <see cref="Name"/> / <see cref="Pid"/> is set after parsing.
/// </summary>
public sealed class ProcessKillOperation : ActionOperation
{
    public override string Id => "process-kill";

    public string? Name { get; init; }
    public int? Pid { get; init; }

    public static ProcessKillOperation FromJson(JsonElement properties)
    {
        if (properties.TryGetProperty("pid", out var pid) && pid.ValueKind == JsonValueKind.Number)
            return new ProcessKillOperation { Pid = pid.GetInt32() };
        if (properties.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
            return new ProcessKillOperation { Name = name.GetString() };
        throw new JsonException("process-kill: neither 'name' nor 'pid' provided");
    }

    public override Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (Pid is int pid)
            {
                using var process = Process.GetProcessById(pid);
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5_000);
                context.Log($"killed pid {pid}");
                return Task.FromResult(OperationResult.Ok("killed 1 process(es)"));
            }

            var name = RenderProperty(Name ?? "", context);
            if (string.IsNullOrWhiteSpace(name))
                return Task.FromResult(OperationResult.Fail("neither 'name' nor 'pid' provided"));

            var killed = 0;
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
