using System.Diagnostics;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Runs a built-in OS tool under <c>%SystemRoot%\System32</c>:
/// <code>
/// new BuiltinToolOperation { Name = "cipher", Args = [$"/w:{drive}"] }
/// </code>
/// A narrowed form of <see cref="ExternalProcessOperation"/>: same argv-style
/// dispatch, but the caller only supplies a tool name. Path separators, drive
/// letters, and parent-directory traversals are rejected; the file is always
/// resolved against <see cref="Environment.SystemDirectory"/>. Prefer this for
/// <c>cipher</c>, <c>sc</c>, <c>ipconfig</c>, etc.: the intent is clearer
/// and the resolution is audit-friendly.
/// </summary>
public sealed class BuiltinToolOperation : ActionOperation
{
    private static readonly char[] s_forbiddenChars = ['\\', '/', ':'];

    public override string Id => "builtin-tool";

    public required string Name { get; init; }
    public IReadOnlyList<string> Args { get; init; } = [];

    /// <summary><see langword="null"/> lets the tool run as long as it needs.</summary>
    public int? TimeoutSeconds { get; init; }

    public override Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        var name = Name.Trim();
        if (string.IsNullOrEmpty(name))
            return Task.FromResult(OperationResult.Fail("empty tool name"));

        if (name.IndexOfAny(s_forbiddenChars) >= 0 || name.Contains("..", StringComparison.Ordinal))
            return Task.FromResult(OperationResult.Fail(
                $"'name' must be a bare tool name under System32, not '{name}'"));

        var filename = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : name + ".exe";
        var fullPath = Path.Combine(Environment.SystemDirectory, filename);
        if (!File.Exists(fullPath))
            return Task.FromResult(OperationResult.Fail($"'{filename}' not found under System32"));

        var psi = new ProcessStartInfo { FileName = fullPath };
        foreach (var arg in Args)
            psi.ArgumentList.Add(arg);

        return ProcessRunner.RunAsync(psi, TimeoutSeconds, context, cancellationToken);
    }
}
