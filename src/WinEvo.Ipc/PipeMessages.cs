using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace WinEvo.Ipc;

/// <summary>
/// JSON envelope: a tagged union discriminated by <c>type</c>.
/// Forward-compatible; unknown types and properties are ignored.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(HandshakeRequest), "handshake")]
[JsonDerivedType(typeof(ExecuteRequest), "execute")]
[JsonDerivedType(typeof(ReadStateRequest), "read-state")]
[JsonDerivedType(typeof(HandshakeResponse), "handshake-response")]
[JsonDerivedType(typeof(ExecutionResponse), "execution-response")]
[JsonDerivedType(typeof(StateResponse), "state-response")]
[JsonDerivedType(typeof(ErrorResponse), "error")]
public abstract class PipeMessage
{
    public string? RequestId { get; init; }
}

public static class PipeProtocol
{
    /// <summary>
    /// Bumped on any change that an older peer would misread. Version 2:
    /// actions are addressed by id instead of shipping their manifest.
    /// </summary>
    public const int Version = 2;
}

public sealed class HandshakeRequest : PipeMessage
{
    public required string ClientKind { get; init; }
    public required string ClientVersion { get; init; }

    // TODO: not validated agent-side yet. AgentHost.HandleAsync answers any
    // handshake regardless of these fields. Enforce the skew checks in
    // docs/ipc-contract.md "Versioning policy" before the Shell and Agent can
    // update independently.
    public int ProtocolVersion { get; init; } = PipeProtocol.Version;
}

public sealed class HandshakeResponse : PipeMessage
{
    public required string AgentVersion { get; init; }
    public required int AgentProtocolVersion { get; init; }
}

/// <summary>Runs the action registered under <see cref="ActionId"/> in the agent's catalog.</summary>
public sealed class ExecuteRequest : PipeMessage
{
    public required string ActionId { get; init; }
    public IReadOnlyDictionary<string, JsonNode?> Parameters { get; init; }
        = new Dictionary<string, JsonNode?>();
}

/// <summary>Asks for the live system values behind an action's parameters.</summary>
public sealed class ReadStateRequest : PipeMessage
{
    public required string ActionId { get; init; }
}

/// <summary>
/// Live values keyed by parameter id. A parameter that is absent has no
/// readable state; the Shell keeps its declared default.
/// </summary>
public sealed class StateResponse : PipeMessage
{
    public IReadOnlyDictionary<string, JsonNode?> Values { get; init; }
        = new Dictionary<string, JsonNode?>();
}

public sealed class ExecutionResponse : PipeMessage
{
    public required bool Success { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<StepResult> StepResults { get; init; } = [];
    public IReadOnlyList<string> Log { get; init; } = [];
}

public sealed class StepResult
{
    public required string Operation { get; init; }
    public required bool Success { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }
}

public sealed class ErrorResponse : PipeMessage
{
    public required string Message { get; init; }
}

public static class PipeMessageSerializer
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static byte[] Serialize(PipeMessage message)
        => JsonSerializer.SerializeToUtf8Bytes<PipeMessage>(message, s_options);

    public static PipeMessage Deserialize(ReadOnlySpan<byte> payload)
    {
        var message = JsonSerializer.Deserialize<PipeMessage>(payload, s_options);
        return message ?? throw new InvalidOperationException("empty pipe message");
    }
}
