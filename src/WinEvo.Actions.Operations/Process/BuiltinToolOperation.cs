using System.Diagnostics;
using System.Text.Json;
using WinEvo.ActionModel;
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
/// This is a narrowed alias of <c>external-process</c> — identical argv-style
/// dispatch, but the caller only supplies a tool name. Path separators, drive
/// letters, and parent-directory traversals are rejected, and the file is
/// always resolved against <see cref="Environment.SystemDirectory"/>. Use
/// this for built-in tools (<c>cipher</c>, <c>sc</c>, <c>ipconfig</c>, …)
/// — it makes the manifest's intent clear and audit- friendly compared to a
/// raw <c>external-process</c> call.
/// </summary>
public sealed class BuiltinToolOperation : IActionOperation
{
    private static readonly char[] s_forbiddenChars = ['\\', '/', ':'];

    public string Id => "builtin-tool";

    public Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        var name = context.RenderProperty("name").Trim();
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
        foreach (var arg in ExtractArgs(context.Step.Properties, context.Parameters))
            psi.ArgumentList.Add(arg);

        return ProcessRunner.RunAsync(psi, context, cancellationToken);
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
}
