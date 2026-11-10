using System.Text.Json.Nodes;
using WinEvo.Ipc;

namespace WinEvo.Shell.Core.Services;

/// <summary>Connected client-side handle to the agent process.</summary>
public interface IAgentClient : IAsyncDisposable
{
    bool IsConnected { get; }

    /// <summary>Performs the agent handshake; returns version info.</summary>
    Task<HandshakeResponse> HandshakeAsync(CancellationToken ct);

    /// <summary>Runs the action with this id in the agent and awaits the full execution result.</summary>
    Task<ExecutionResponse> ExecuteAsync(string actionId, IReadOnlyDictionary<string, JsonNode?> parameters, CancellationToken ct);

    /// <summary>
    /// Reads the live system values behind the action's parameters, keyed by
    /// parameter id. Parameters with no readable state are absent.
    /// </summary>
    Task<IReadOnlyDictionary<string, JsonNode?>> ReadStateAsync(string actionId, CancellationToken ct);
}
