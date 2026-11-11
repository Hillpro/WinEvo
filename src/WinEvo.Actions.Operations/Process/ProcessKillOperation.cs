using System.Diagnostics;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Kills a process by name or PID:
/// <code>
/// new ProcessKillOperation { Name = "SearchHost" }
/// new ProcessKillOperation { Pid = 1234 }
/// </code>
/// Set exactly one of <see cref="Name"/> / <see cref="Pid"/>; when both are
/// set, <see cref="Pid"/> wins. Nothing to kill is a success.
/// </summary>
public sealed class ProcessKillOperation : ActionOperation
{
    public override string Id => "process-kill";

    public string? Name { get; init; }
    public int? Pid { get; init; }

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

            if (string.IsNullOrWhiteSpace(Name))
                return Task.FromResult(OperationResult.Fail("neither 'name' nor 'pid' provided"));

            var killed = 0;
            var processes = Process.GetProcessesByName(Name);
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
                    context.Log($"failed to kill '{Name}' (pid {process.Id}): {ex.Message}");
                }
                finally
                {
                    process.Dispose();
                }
            }
            context.Log($"killed {killed} process(es) named '{Name}'");
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
