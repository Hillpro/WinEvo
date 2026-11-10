using System.Diagnostics;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Runs an external executable with argv-style arguments:
/// <code>
/// new ExternalProcessOperation
/// {
///     Path = @"%ProgramFiles%\Vendor\tool.exe",
///     Args = ["/quiet", drive],
///     TimeoutSeconds = 60,
/// }
/// </code>
/// <c>%VARIABLE%</c> references in <see cref="Path"/> are expanded. Each entry
/// of <see cref="Args"/> reaches the process as one argument, unparsed, so a
/// value coming from user input cannot inject extra arguments. For a tool
/// under System32, prefer <see cref="BuiltinToolOperation"/>.
/// </summary>
public sealed class ExternalProcessOperation : ActionOperation
{
    public override string Id => "external-process";

    public required string Path { get; init; }
    public IReadOnlyList<string> Args { get; init; } = [];

    /// <summary><see langword="null"/> lets the process run as long as it needs.</summary>
    public int? TimeoutSeconds { get; init; }

    public override Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        var path = Environment.ExpandEnvironmentVariables(Path);
        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult(OperationResult.Fail("empty executable path"));

        var psi = new ProcessStartInfo { FileName = path };
        foreach (var arg in Args)
            psi.ArgumentList.Add(arg);

        return ProcessRunner.RunAsync(psi, TimeoutSeconds, context, cancellationToken);
    }
}
