using System.Diagnostics;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Runs a command through <c>cmd.exe /C</c>:
/// <code>
/// new CommandOperation { Command = "cleanmgr /sagerun:1", TimeoutSeconds = 60 }
/// </code>
/// Use this when you need shell features — built-ins (<c>dir</c>, <c>copy</c>,
/// <c>del</c>), <c>&amp;&amp;</c> / <c>||</c> chaining, pipes, redirections,
/// or <c>.cmd</c>/<c>.bat</c> scripts. For a plain executable with structured
/// arguments, prefer <see cref="ExternalProcessOperation"/>: it avoids
/// cmd.exe's quoting quirks and doesn't spin up a shell interpreter.
///
/// <para>
/// The command text is executed as written. Never build it by concatenating
/// a parameter value the user typed: that value would run as shell code.
/// </para>
/// </summary>
public sealed class CommandOperation : ActionOperation
{
    public override string Id => "command";

    public required string Command { get; init; }

    /// <summary><see langword="null"/> lets the command run as long as it needs.</summary>
    public int? TimeoutSeconds { get; init; }

    public override Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Command))
            return Task.FromResult(OperationResult.Fail("empty command"));

        // Pass the command via Arguments (not ArgumentList) so cmd.exe receives
        // the tail verbatim and can parse pipes, redirections, and quoting as
        // the action author wrote them. ArgumentList would wrap the whole
        // command string in quotes, which cmd.exe's /C parser handles in
        // surprising ways when special characters are present.
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/C {Command}",
        };

        return ProcessRunner.RunAsync(psi, TimeoutSeconds, context, cancellationToken);
    }
}
