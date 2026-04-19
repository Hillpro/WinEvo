using System.Text.Json;
using WinEvo.ActionModel;

namespace WinEvo.Actions.Abstractions;

/// <summary>
/// Per-step execution context. Holds the step definition, resolved parameter
/// values, and an event sink the operation can use to emit progress/logs.
/// </summary>
public sealed class OperationContext
{
    /// <summary>The operation step currently being executed.</summary>
    public required OperationStep Step { get; init; }

    /// <summary>
    /// Parameters bound for this action invocation. Keys match the parameter
    /// <c>id</c> declared in the manifest; values are the CLR representation
    /// produced by <c>ActionExecutor.BindParameters</c>.
    /// </summary>
    public required IReadOnlyDictionary<string, object?> Parameters { get; init; }

    /// <summary>Optional sink for streaming log lines back to the caller.</summary>
    public Action<string>? LogSink { get; init; }

    /// <summary>Writes a single log line to <see cref="LogSink"/> if one is attached.</summary>
    public void Log(string message) => LogSink?.Invoke(message);

    /// <summary>
    /// Reads a string property off <see cref="Step"/>'s raw JSON and renders
    /// any <c>{{params.X}}</c> / <c>%EnvVar%</c> expressions found in it.
    /// Returns <paramref name="fallback"/> when the property is missing or
    /// not a JSON string.
    /// </summary>
    public string RenderProperty(string propertyName, string fallback = "")
    {
        if (!Step.Properties.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return fallback;
        }
        return Templating.Render(value.GetString() ?? fallback, Parameters);
    }
}
