using System.Diagnostics;
using System.Text.Json;
using WinEvo.ActionModel;
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
public sealed class ExternalProcessOperation : IActionOperation
{
    public string Id => "external-process";

    public Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
    {
        var path = context.RenderProperty("path");
        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult(OperationResult.Fail("missing 'path' property"));

        var psi = new ProcessStartInfo { FileName = path };
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
