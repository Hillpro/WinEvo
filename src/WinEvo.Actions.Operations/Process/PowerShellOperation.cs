using System.Diagnostics;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Runs a PowerShell script:
/// <code>
/// new PowerShellOperation
/// {
///     Script = "Get-Service wuauserv | Set-Service -StartupType Disabled",
///     TimeoutSeconds = 60,
/// }
/// </code>
/// Uses Windows PowerShell 5.1 (<c>powershell.exe</c>) — always present on
/// Windows 10/11 and doesn't require shipping pwsh. The script runs with
/// <c>-NoProfile -NonInteractive -ExecutionPolicy Bypass</c> so the user's
/// profile and execution policy don't change the outcome.
///
/// <para>
/// The script text is executed as written. Never build it by concatenating a
/// parameter value the user typed: that value would run as PowerShell code.
/// </para>
/// </summary>
public sealed class PowerShellOperation : ActionOperation
{
    public override string Id => "powershell";

    public required string Script { get; init; }

    /// <summary><see langword="null"/> lets the script run as long as it needs.</summary>
    public int? TimeoutSeconds { get; init; }

    public override Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Script))
            return Task.FromResult(OperationResult.Fail("empty script"));

        var psi = new ProcessStartInfo { FileName = "powershell.exe" };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(Script);

        return ProcessRunner.RunAsync(psi, TimeoutSeconds, context, cancellationToken);
    }
}
