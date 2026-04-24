using System.Diagnostics;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Runs a command through <c>cmd.exe /C</c>. Manifest shape:
/// <code>
/// { "operation": "command",
///   "command": "cleanmgr /sagerun:1",
///   "timeout": 60 }
/// </code>
/// Use this when you need shell features — built-ins (<c>dir</c>, <c>copy</c>,
/// <c>del</c>), <c>&amp;&amp;</c> / <c>||</c> chaining, pipes, redirections,
/// or <c>.cmd</c>/<c>.bat</c> scripts. For a plain executable with structured
/// arguments, prefer <c>external-process</c>: it avoids cmd.exe's quoting
/// quirks and doesn't spin up a shell interpreter.
/// </summary>
public sealed class CommandOperation : IActionOperation
{
    public string Id => "command";

    public Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        var command = context.RenderProperty("command");
        if (string.IsNullOrWhiteSpace(command))
            return Task.FromResult(OperationResult.Fail("missing 'command' property"));

        // Pass the command via Arguments (not ArgumentList) so cmd.exe receives
        // the tail verbatim and can parse pipes, redirections, and quoting as
        // the manifest author wrote them. ArgumentList would wrap the whole
        // command string in quotes, which cmd.exe's /C parser handles in
        // surprising ways when special characters are present.
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/C {command}",
        };

        return ProcessRunner.RunAsync(psi, context, cancellationToken);
    }
}
