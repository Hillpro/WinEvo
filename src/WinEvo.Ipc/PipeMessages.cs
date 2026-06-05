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
[JsonDerivedType(typeof(HandshakeResponse), "handshake-response")]
[JsonDerivedType(typeof(ExecutionResponse), "execution-response")]
[JsonDerivedType(typeof(ErrorResponse), "error")]
public abstract class PipeMessage
{
    public string? RequestId { get; init; }
}

public sealed class HandshakeRequest : PipeMessage
{
    public required string ClientKind { get; init; }
    public required string ClientVersion { get; init; }

    // TODO: not validated agent-side yet. AgentHost.HandleAsync answers any
    // handshake regardless of these fields. Enforce the skew checks in
    // docs/ipc-contract.md "Versioning policy" before the Shell and Agent can
    // update independently.
    public int ProtocolVersion { get; init; } = 1;
}

public sealed class HandshakeResponse : PipeMessage
{
    public required string AgentVersion { get; init; }
    public required int AgentProtocolVersion { get; init; }
    public required IReadOnlyList<string> SupportedOperations { get; init; }
}

public sealed class ExecuteRequest : PipeMessage
{
    public required JsonNode Manifest { get; init; }
    public IReadOnlyDictionary<string, JsonNode?> Parameters { get; init; }
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
    public required string? StepId { get; init; }
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
