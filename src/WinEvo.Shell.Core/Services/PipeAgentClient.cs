using System.IO.Pipes;
using System.Text.Json.Nodes;
using WinEvo.Ipc;

namespace WinEvo.Shell.Core.Services;

/// <summary>
/// Agent client that talks to the broker over a local named pipe using the
/// JSON framing defined in <see cref="WinEvo.Ipc.PipeFraming"/>. TODO: swap to
/// a gRPC client over the same pipe once the <c>.proto</c> service is
/// implemented on the agent side.
/// </summary>
public sealed class PipeAgentClient : IAgentClient
{
    private readonly string _pipeName;
    private NamedPipeClientStream? _stream;

    public PipeAgentClient(string pipeName)
    {
        _pipeName = pipeName;
    }

    public bool IsConnected => _stream?.IsConnected == true;

    public async Task ConnectAsync(TimeSpan timeout, CancellationToken ct)
    {
        _stream = new NamedPipeClientStream(
            ".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await _stream.ConnectAsync((int)timeout.TotalMilliseconds, ct).ConfigureAwait(false);
    }

    public async Task<HandshakeResponse> HandshakeAsync(CancellationToken ct)
    {
        var request = new HandshakeRequest
        {
            RequestId = Guid.NewGuid().ToString("N"),
            ClientKind = "shell",
            ClientVersion = typeof(PipeAgentClient).Assembly.GetName().Version?.ToString() ?? "0.1.0",
        };
        var response = await SendAsync(request, ct).ConfigureAwait(false);
        return response switch
        {
            HandshakeResponse ok => ok,
            ErrorResponse err => throw new InvalidOperationException($"handshake failed: {err.Message}"),
            _ => throw new InvalidOperationException($"unexpected handshake response"),
        };
    }

    public async Task<ExecutionResponse> ExecuteAsync(
        JsonNode manifest,
        IReadOnlyDictionary<string, JsonNode?> parameters,
        CancellationToken ct)
    {
        var request = new ExecuteRequest
        {
            RequestId = Guid.NewGuid().ToString("N"),
            Manifest = manifest,
            Parameters = parameters,
        };
        var response = await SendAsync(request, ct).ConfigureAwait(false);
        return response switch
        {
            ExecutionResponse ok => ok,
            ErrorResponse err => throw new InvalidOperationException($"execute failed: {err.Message}"),
            _ => throw new InvalidOperationException($"unexpected execute response"),
        };
    }

    private async Task<PipeMessage> SendAsync(PipeMessage request, CancellationToken ct)
    {
        if (_stream is null || !_stream.IsConnected)
            throw new InvalidOperationException("agent pipe not connected");

        var bytes = PipeMessageSerializer.Serialize(request);
        await PipeFraming.WriteFrameAsync(_stream, bytes, ct).ConfigureAwait(false);

        var frame = await PipeFraming.ReadFrameAsync(_stream, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("agent closed pipe");
        return PipeMessageSerializer.Deserialize(frame);
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream is not null)
        {
            try { await _stream.DisposeAsync().ConfigureAwait(false); }
            catch { /* best-effort */ }
            _stream = null;
        }
    }
}
