using System.Diagnostics;
using System.Text.Json;
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
public sealed class CommandOperation : ActionOperation
{
    public override string Id => "command";

    public required string Command { get; init; }
    public int? TimeoutSeconds { get; init; }

    public static CommandOperation FromJson(JsonElement properties)
    {
        if (!properties.TryGetProperty("command", out var cmd) || cmd.ValueKind != JsonValueKind.String)
            throw new JsonException("command: missing or non-string 'command' property");
        return new CommandOperation
        {
            Command = cmd.GetString()!,
            TimeoutSeconds = ExternalProcessOperation.ParseTimeout(properties),
        };
    }

    public override Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        var command = RenderProperty(Command, context);
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

        return ProcessRunner.RunAsync(psi, TimeoutSeconds, context, cancellationToken);
    }
}
