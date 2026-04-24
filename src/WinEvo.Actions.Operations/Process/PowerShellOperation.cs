using System.Diagnostics;
using System.Text.Json;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Runs a PowerShell script. Manifest shape:
/// <code>
/// { "operation": "powershell",
///   "script": "Get-Service wuauserv | Set-Service -StartupType Disabled",
///   "timeout": 60 }
/// </code>
/// Uses Windows PowerShell 5.1 (<c>powershell.exe</c>) — always present on
/// Windows 10/11 and doesn't require shipping pwsh. The script runs with
/// <c>-NoProfile -NonInteractive -ExecutionPolicy Bypass</c> so the user's
/// profile and execution policy don't change the outcome. Template expressions
/// <c>{{params.X}}</c> in the script are rendered before dispatch.
/// </summary>
public sealed class PowerShellOperation : ActionOperation
{
    public override string Id => "powershell";

    public required string Script { get; init; }
    public int? TimeoutSeconds { get; init; }

    public static PowerShellOperation FromJson(JsonElement properties)
    {
        if (!properties.TryGetProperty("script", out var s) || s.ValueKind != JsonValueKind.String)
            throw new JsonException("powershell: missing or non-string 'script' property");
        return new PowerShellOperation
        {
            Script = s.GetString()!,
            TimeoutSeconds = ExternalProcessOperation.ParseTimeout(properties),
        };
    }

    public override Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        var script = RenderProperty(Script, context);
        if (string.IsNullOrWhiteSpace(script))
            return Task.FromResult(OperationResult.Fail("missing 'script' property"));

        var psi = new ProcessStartInfo { FileName = "powershell.exe" };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(script);

        return ProcessRunner.RunAsync(psi, TimeoutSeconds, context, cancellationToken);
    }
}
