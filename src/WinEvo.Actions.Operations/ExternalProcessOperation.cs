using System.Diagnostics;
using System.Text;
using System.Text.Json;
using WinEvo.ActionModel;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Runs an external executable with argv-style arguments. Manifest shape:
/// <code>
/// { "operation": "external-process",
///   "path": "%SystemRoot%\\System32\\cipher.exe",
///   "args": ["/w:{{params.drive}}"],
///   "timeout": 60 }
/// </code>
/// </summary>
public sealed class ExternalProcessOperation : IActionOperation
{
    public string Id => "external-process";

    // Varies by executable; runtime doesn't know, so default to false.
    public bool RequiresElevation => false;

    public async Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        var path = context.RenderProperty("path");
        if (string.IsNullOrWhiteSpace(path))
            return OperationResult.Fail("missing 'path' property");

        var args = ExtractArgs(context.Step.Properties, context.Parameters);
        var timeoutSeconds = context.Step.Properties.TryGetProperty("timeout", out var t)
            && t.ValueKind == JsonValueKind.Number
                ? t.GetInt32()
                : 0;

        var psi = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        try
        {
            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

            context.Log($"starting {path} {string.Join(' ', args)}");
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var timeoutCts = timeoutSeconds > 0
                ? new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds))
                : null;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts?.Token ?? CancellationToken.None);

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

    private static string[] ExtractArgs(JsonElement props, IReadOnlyDictionary<string, object?> parameters)
    {
        if (!props.TryGetProperty("args", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];

        return arr.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => Templating.Render(e.GetString() ?? "", parameters))
            .ToArray();
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch { /* best-effort */ }
    }
}
