using System.Diagnostics;
using System.Text.Json;
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
public sealed class ExternalProcessOperation : ActionOperation
{
    public override string Id => "external-process";

    public required string Path { get; init; }
    public IReadOnlyList<string> Args { get; init; } = [];
    public int? TimeoutSeconds { get; init; }

    public static ExternalProcessOperation FromJson(JsonElement properties)
    {
        if (!properties.TryGetProperty("path", out var pathProp) || pathProp.ValueKind != JsonValueKind.String)
            throw new JsonException("external-process: missing or non-string 'path' property");
        return new ExternalProcessOperation
        {
            Path = pathProp.GetString()!,
            Args = ParseArgs(properties),
            TimeoutSeconds = ParseTimeout(properties),
        };
    }

    public override Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        var path = RenderProperty(Path, context);
        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult(OperationResult.Fail("missing 'path' property"));

        var psi = new ProcessStartInfo { FileName = path };
        foreach (var arg in Args)
            psi.ArgumentList.Add(RenderProperty(arg, context));

        return ProcessRunner.RunAsync(psi, TimeoutSeconds, context, cancellationToken);
    }

    internal static string[] ParseArgs(JsonElement properties)
    {
        if (!properties.TryGetProperty("args", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];
        return arr.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString() ?? "")
            .ToArray();
    }

    internal static int? ParseTimeout(JsonElement properties)
        => properties.TryGetProperty("timeout", out var t) && t.ValueKind == JsonValueKind.Number
            ? t.GetInt32()
            : null;
}
