using System.Text.Json.Nodes;
using WinEvo.Ipc;

namespace WinEvo.Shell.Core.Services;

/// <summary>Connected client-side handle to the agent process.</summary>
public interface IAgentClient : IAsyncDisposable
{
    bool IsConnected { get; }

    /// <summary>Performs the agent handshake; returns supported operations + version info.</summary>
    Task<HandshakeResponse> HandshakeAsync(CancellationToken ct);

    /// <summary>Sends an action manifest + parameters and awaits the full execution result.</summary>
    Task<ExecutionResponse> ExecuteAsync(JsonNode manifest, IReadOnlyDictionary<string, JsonNode?> parameters, CancellationToken ct);
}
