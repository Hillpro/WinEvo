using System.IO.Pipes;
using System.Text.Json;
using WinEvo.ActionModel;
using WinEvo.Actions.Operations;
using WinEvo.Ipc;

namespace WinEvo.Agent.Core;

/// <summary>
/// Agent-side loop: hosts a named-pipe server, accepts one client at a time
/// (the Shell), and dispatches inbound <see cref="PipeMessage"/> requests to
/// the <see cref="ActionExecutor"/>. When the pipe closes, the agent exits.
/// </summary>
public sealed class AgentHost
{
    private readonly string _pipeName;
    private readonly OperationCatalog _operations;
    private readonly ActionExecutor _executor;

    public AgentHost(string pipeName, OperationCatalog? operations = null)
    {
        _pipeName = pipeName;
        _operations = operations ?? OperationCatalog.Default();
        _executor = new ActionExecutor(_operations);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        using var server = new NamedPipeServerStream(
            _pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        Console.WriteLine($"agent: listening on pipe '{_pipeName}'");
        await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
        Console.WriteLine("agent: client connected");

        try
        {
            while (server.IsConnected)
            {
                ct.ThrowIfCancellationRequested();
                var frame = await PipeFraming.ReadFrameAsync(server, ct).ConfigureAwait(false);
                if (frame is null) break;

                PipeMessage response;
                try
                {
                    var request = PipeMessageSerializer.Deserialize(frame);
                    response = await HandleAsync(request, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    response = new ErrorResponse { Message = $"{ex.GetType().Name}: {ex.Message}" };
                }

                var bytes = PipeMessageSerializer.Serialize(response);
                await PipeFraming.WriteFrameAsync(server, bytes, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* graceful */ }
        finally
        {
            Console.WriteLine("agent: client disconnected, shutting down");
        }
    }

    private async Task<PipeMessage> HandleAsync(PipeMessage request, CancellationToken ct) => request switch
    {
        HandshakeRequest hs => new HandshakeResponse
        {
            RequestId = hs.RequestId,
            AgentVersion = typeof(AgentHost).Assembly.GetName().Version?.ToString() ?? "0.1.0",
            AgentProtocolVersion = 1,
            SupportedOperations = _operations.SupportedIds.ToArray(),
        },
        ExecuteRequest exec => await HandleExecuteAsync(exec, ct).ConfigureAwait(false),
        _ => new ErrorResponse { RequestId = request.RequestId, Message = "unsupported message type" },
    };

    private async Task<PipeMessage> HandleExecuteAsync(ExecuteRequest request, CancellationToken ct)
    {
        var manifestJson = request.Manifest.ToJsonString();
        var manifest = ManifestLoader.Parse(manifestJson);

        // Materialise each parameter into a document-independent JsonElement so
        // the transient JsonDocuments can be disposed immediately (pool return).
        var rawParams = new Dictionary<string, JsonElement>();
        foreach (var (k, v) in request.Parameters)
        {
            var json = v?.ToJsonString() ?? "null";
            using var doc = JsonDocument.Parse(json);
            rawParams[k] = doc.RootElement.Clone();
        }

        var bound = ActionExecutor.BindParameters(manifest, rawParams);
        return await _executor.ExecuteAsync(manifest, bound, request.RequestId, ct).ConfigureAwait(false);
    }
}
