using System.Text.Json;

namespace WinEvo.Actions.Abstractions;

/// <summary>
/// Turns the raw (<c>operation</c> id + properties JSON) pair carried by a
/// manifest's <c>OperationStep</c> into a hydrated <see cref="ActionOperation"/>
/// ready to execute. Unknown ids return a fallback that fails at execute time,
/// so forward-compatible manifests (ones that name an operation this agent
/// doesn't implement) still parse successfully.
/// </summary>
public interface IOperationParser
{
    /// <summary>
    /// Constructs the concrete <see cref="ActionOperation"/> for <paramref name="operationId"/>,
    /// binding its fields from <paramref name="properties"/>. Never returns null —
    /// unknown ids yield a fallback that reports "not implemented" when executed.
    /// </summary>
    ActionOperation Parse(string operationId, JsonElement properties);

    /// <summary>Operation ids this parser knows how to hydrate. Surfaces in the handshake's <c>supported_operations</c>.</summary>
    IReadOnlyCollection<string> SupportedIds { get; }
}
