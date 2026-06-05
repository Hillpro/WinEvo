using System.Text.Json;
using System.Text.Json.Nodes;
using WinEvo.Ipc;

namespace WinEvo.Ipc.Tests;

/// <summary>
/// Locks the polymorphic JSON envelope: every message subtype must round-trip
/// to its concrete type, the discriminator must be the camelCase <c>type</c>
/// tag, property names must be camelCase, and null fields must be omitted.
/// A renamed property or a forgotten [JsonDerivedType] breaks these.
/// </summary>
public class PipeMessageSerializerTests
{
    private static T RoundTrip<T>(T message) where T : PipeMessage
        => Assert.IsType<T>(PipeMessageSerializer.Deserialize(PipeMessageSerializer.Serialize(message)));

    [Fact]
    public void HandshakeRequest_round_trips()
    {
        var result = RoundTrip(new HandshakeRequest
        {
            RequestId = "r1",
            ClientKind = "shell",
            ClientVersion = "0.2.0",
            ProtocolVersion = 1,
        });

        Assert.Equal("r1", result.RequestId);
        Assert.Equal("shell", result.ClientKind);
        Assert.Equal("0.2.0", result.ClientVersion);
        Assert.Equal(1, result.ProtocolVersion);
    }

    [Fact]
    public void HandshakeResponse_round_trips_with_operation_list()
    {
        var result = RoundTrip(new HandshakeResponse
        {
            RequestId = "r2",
            AgentVersion = "0.2.0",
            AgentProtocolVersion = 1,
            SupportedOperations = ["registry-set", "delay"],
        });

        Assert.Equal("0.2.0", result.AgentVersion);
        Assert.Equal(["registry-set", "delay"], result.SupportedOperations);
    }

    [Fact]
    public void ExecuteRequest_round_trips_manifest_and_typed_parameters()
    {
        var result = RoundTrip(new ExecuteRequest
        {
            RequestId = "r3",
            Manifest = JsonNode.Parse("""{"id":"x","version":"1.0.0"}""")!,
            Parameters = new Dictionary<string, JsonNode?>
            {
                ["flag"] = JsonValue.Create(true),
                ["count"] = JsonValue.Create(7),
                ["name"] = JsonValue.Create("disk"),
            },
        });

        Assert.Equal("x", result.Manifest["id"]!.GetValue<string>());
        Assert.True(result.Parameters["flag"]!.GetValue<bool>());
        Assert.Equal(7, result.Parameters["count"]!.GetValue<int>());
        Assert.Equal("disk", result.Parameters["name"]!.GetValue<string>());
    }

    [Fact]
    public void ExecutionResponse_round_trips_step_results_and_log()
    {
        var result = RoundTrip(new ExecutionResponse
        {
            RequestId = "r4",
            Success = false,
            Message = "failed at step 1 of 2",
            StepResults =
            [
                new StepResult { StepId = "s1", Operation = "registry-set", Success = false, Error = "access denied" },
                new StepResult { StepId = "s2", Operation = "delay", Success = true, Message = "waited" },
            ],
            Log = ["line one", "line two"],
        });

        Assert.False(result.Success);
        Assert.Equal(2, result.StepResults.Count);
        Assert.Equal("registry-set", result.StepResults[0].Operation);
        Assert.Equal("access denied", result.StepResults[0].Error);
        Assert.Equal(["line one", "line two"], result.Log);
    }

    [Fact]
    public void ErrorResponse_round_trips()
    {
        var result = RoundTrip(new ErrorResponse { RequestId = "r5", Message = "unsupported message type" });
        Assert.Equal("unsupported message type", result.Message);
    }

    [Fact]
    public void Discriminator_and_property_names_are_camelCase()
    {
        var bytes = PipeMessageSerializer.Serialize(new HandshakeRequest
        {
            ClientKind = "shell",
            ClientVersion = "0.2.0",
        });

        using var doc = JsonDocument.Parse(bytes);
        Assert.Equal("handshake", doc.RootElement.GetProperty("type").GetString());
        Assert.True(doc.RootElement.TryGetProperty("clientKind", out _));
        Assert.True(doc.RootElement.TryGetProperty("clientVersion", out _));
    }

    [Fact]
    public void Null_fields_are_omitted()
    {
        // RequestId is null by default; WhenWritingNull should drop it from the wire.
        var bytes = PipeMessageSerializer.Serialize(new ErrorResponse { Message = "boom" });

        using var doc = JsonDocument.Parse(bytes);
        Assert.False(doc.RootElement.TryGetProperty("requestId", out _));
        Assert.Equal("boom", doc.RootElement.GetProperty("message").GetString());
    }
}
