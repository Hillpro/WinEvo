using System.Diagnostics;
using System.Text;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Shared child-process runner used by the operations that spawn an executable
/// (<c>external-process</c>, <c>powershell</c>, <c>command</c>, <c>builtin-tool</c>).
/// Takes a caller-built <see cref="ProcessStartInfo"/>, forces stdio redirection,
/// honors both the step-level timeout and the outer cancellation token, and
/// maps the exit code to a uniform <see cref="OperationResult"/>. The child is
/// killed on timeout or cancellation; the agent's Job Object
/// (<see cref="WinEvo.Agent.Core.JobObject"/>) then propagates the kill to any
/// grandchildren the script may have spawned.
/// </summary>
internal static class ProcessRunner
{
    public static async Task<OperationResult> RunAsync(
        ProcessStartInfo psi,
        int? timeoutSeconds,
        OperationContext context,
        CancellationToken ct)
    {
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.CreateNoWindow = true;

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        try
        {
            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

            context.Log($"starting {psi.FileName} {FormatArgs(psi)}".TrimEnd());
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var timeoutCts = timeoutSeconds > 0
                ? new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds.Value))
                : null;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                ct, timeoutCts?.Token ?? CancellationToken.None);

            try
            {
                await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                if (timeoutCts?.IsCancellationRequested == true)
                    return OperationResult.Fail($"timed out after {timeoutSeconds}s");
                throw;
            }
            finally
            {
                timeoutCts?.Dispose();
            }

            var exit = process.ExitCode;
            context.Log($"exit code {exit}");

            return exit == 0
                ? OperationResult.Ok($"exit 0 ({stdout.Length} bytes stdout)")
                : OperationResult.Fail($"exit {exit}", stderr.ToString().Trim());
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.GetType().Name, ex.Message);
        }
    }

    private static string FormatArgs(ProcessStartInfo psi)
        => psi.ArgumentList.Count > 0 ? string.Join(' ', psi.ArgumentList) : psi.Arguments;

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch { /* best-effort */ }
    }
}
