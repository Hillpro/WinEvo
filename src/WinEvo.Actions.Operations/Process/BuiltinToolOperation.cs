using System.Diagnostics;
using System.Text.Json;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations;

/// <summary>
/// Runs a built-in OS tool under <c>%SystemRoot%\System32</c>. Manifest shape:
/// <code>
/// { "operation": "builtin-tool",
///   "name": "cipher",
///   "args": ["/w:{{params.drive}}"],
///   "timeout": 60 }
/// </code>
/// A narrowed alias of <c>external-process</c>: same argv-style dispatch, but
/// the caller only supplies a tool name. Path separators, drive letters, and
/// parent-directory traversals are rejected; the file is always resolved
/// against <see cref="Environment.SystemDirectory"/>. Prefer this for
/// <c>cipher</c>, <c>sc</c>, <c>ipconfig</c>, etc. — the manifest's intent is
/// clearer and the resolution is audit-friendly.
/// </summary>
public sealed class BuiltinToolOperation : ActionOperation
{
    private static readonly char[] s_forbiddenChars = ['\\', '/', ':'];

    public override string Id => "builtin-tool";

    public required string Name { get; init; }
    public IReadOnlyList<string> Args { get; init; } = [];
    public int? TimeoutSeconds { get; init; }

    public static BuiltinToolOperation FromJson(JsonElement properties)
    {
        if (!properties.TryGetProperty("name", out var nameProp) || nameProp.ValueKind != JsonValueKind.String)
            throw new JsonException("builtin-tool: missing or non-string 'name' property");
        return new BuiltinToolOperation
        {
            Name = nameProp.GetString()!,
            Args = ExternalProcessOperation.ParseArgs(properties),
            TimeoutSeconds = ExternalProcessOperation.ParseTimeout(properties),
        };
    }

    public override Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        var name = RenderProperty(Name, context).Trim();
        if (string.IsNullOrEmpty(name))
            return Task.FromResult(OperationResult.Fail("missing 'name' property"));

        if (name.IndexOfAny(s_forbiddenChars) >= 0 || name.Contains("..", StringComparison.Ordinal))
            return Task.FromResult(OperationResult.Fail(
                $"'name' must be a bare tool name under System32, not '{name}'"));

        var filename = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : name + ".exe";
        var fullPath = Path.Combine(Environment.SystemDirectory, filename);
        if (!File.Exists(fullPath))
            return Task.FromResult(OperationResult.Fail($"'{filename}' not found under System32"));

        var psi = new ProcessStartInfo { FileName = fullPath };
        foreach (var arg in Args)
            psi.ArgumentList.Add(RenderProperty(arg, context));

        return ProcessRunner.RunAsync(psi, TimeoutSeconds, context, cancellationToken);
    }
}
