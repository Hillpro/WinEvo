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
            ProtocolVersion = 2,
        });

        Assert.Equal("r1", result.RequestId);
        Assert.Equal("shell", result.ClientKind);
        Assert.Equal("0.2.0", result.ClientVersion);
        Assert.Equal(2, result.ProtocolVersion);
    }

    [Fact]
    public void HandshakeRequest_defaults_to_the_current_protocol_version()
    {
        var request = new HandshakeRequest { ClientKind = "shell", ClientVersion = "0.2.0" };

        Assert.Equal(PipeProtocol.Version, request.ProtocolVersion);
    }

    [Fact]
    public void HandshakeResponse_round_trips()
    {
        var result = RoundTrip(new HandshakeResponse
        {
            RequestId = "r2",
            AgentVersion = "0.2.0",
            AgentProtocolVersion = 2,
        });

        Assert.Equal("0.2.0", result.AgentVersion);
        Assert.Equal(2, result.AgentProtocolVersion);
    }

    [Fact]
    public void ExecuteRequest_round_trips_action_id_and_typed_parameters()
    {
        var result = RoundTrip(new ExecuteRequest
        {
            RequestId = "r3",
            ActionId = "storage.wipe-free-space",
            Parameters = new Dictionary<string, JsonNode?>
            {
                ["flag"] = JsonValue.Create(true),
                ["count"] = JsonValue.Create(7),
                ["name"] = JsonValue.Create("disk"),
            },
        });

        Assert.Equal("storage.wipe-free-space", result.ActionId);
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
            Message = "failed at step 1 of 3",
            StepResults =
            [
                new StepResult { Operation = "registry-set", Success = false, Error = "access denied" },
                new StepResult { Operation = "delay", Success = true, Message = "waited" },
                new StepResult { Operation = "process-kill", Success = false, Error = "denied", FailureTolerated = true },
            ],
            Log = ["line one", "line two"],
        });

        Assert.False(result.Success);
        Assert.Equal(3, result.StepResults.Count);
        Assert.Equal("registry-set", result.StepResults[0].Operation);
        Assert.Equal("access denied", result.StepResults[0].Error);
        Assert.False(result.StepResults[0].FailureTolerated);
        Assert.True(result.StepResults[2].FailureTolerated);
        Assert.Equal(["line one", "line two"], result.Log);
    }

    [Fact]
    public void ReadStateRequest_round_trips()
    {
        var result = RoundTrip(new ReadStateRequest { RequestId = "r6", ActionId = "customization.bing-search-results" });

        Assert.Equal("r6", result.RequestId);
        Assert.Equal("customization.bing-search-results", result.ActionId);
    }

    [Fact]
    public void StateResponse_round_trips_typed_values()
    {
        var result = RoundTrip(new StateResponse
        {
            RequestId = "r7",
            Values = new Dictionary<string, JsonNode?>
            {
                ["isOn"] = JsonValue.Create(false),
                ["level"] = JsonValue.Create("high"),
            },
        });

        Assert.False(result.Values["isOn"]!.GetValue<bool>());
        Assert.Equal("high", result.Values["level"]!.GetValue<string>());
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
