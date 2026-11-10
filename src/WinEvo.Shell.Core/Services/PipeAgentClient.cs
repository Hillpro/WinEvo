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

    // The pipe carries one request and its response at a time; a second
    // caller writing or reading in between would get the wrong frame.
    private readonly SemaphoreSlim _oneRequestAtATime = new(1, 1);

    private NamedPipeClientStream? _stream;

    public PipeAgentClient(string pipeName)
    {
        _pipeName = pipeName;
    }

    public bool IsConnected => _stream?.IsConnected == true;

    public async Task ConnectAsync(TimeSpan timeout, CancellationToken ct)
    {
        // Dispose any prior stream (e.g. failed attempt from the retry loop)
        // before allocating a new one. Leaving _stream null until the new
        // stream is fully connected keeps IsConnected honest on throw paths.
        if (_stream is not null)
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
            _stream = null;
        }

        var stream = new NamedPipeClientStream(
            ".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await stream.ConnectAsync((int)timeout.TotalMilliseconds, ct).ConfigureAwait(false);
            _stream = stream;
        }
        catch
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<HandshakeResponse> HandshakeAsync(CancellationToken ct)
    {
        var request = new HandshakeRequest
        {
            RequestId = Guid.NewGuid().ToString("N"),
            ClientKind = "shell",
            ClientVersion = typeof(PipeAgentClient).Assembly.GetName().Version?.ToString() ?? "0.0.0",
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
        string actionId,
        IReadOnlyDictionary<string, JsonNode?> parameters,
        CancellationToken ct)
    {
        var request = new ExecuteRequest
        {
            RequestId = Guid.NewGuid().ToString("N"),
            ActionId = actionId,
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

    public async Task<IReadOnlyDictionary<string, JsonNode?>> ReadStateAsync(string actionId, CancellationToken ct)
    {
        var request = new ReadStateRequest
        {
            RequestId = Guid.NewGuid().ToString("N"),
            ActionId = actionId,
        };
        var response = await SendAsync(request, ct).ConfigureAwait(false);
        return response switch
        {
            StateResponse ok => ok.Values,
            ErrorResponse err => throw new InvalidOperationException($"read state failed: {err.Message}"),
            _ => throw new InvalidOperationException($"unexpected read-state response"),
        };
    }

    /// <summary>
    /// Sends one request and returns its response. <paramref name="ct"/> only
    /// cancels the wait for the pipe to be free: once the request is written
    /// its response is always read, because an unread response would be
    /// handed to the next caller as if it were theirs.
    /// </summary>
    private async Task<PipeMessage> SendAsync(PipeMessage request, CancellationToken ct)
    {
        await _oneRequestAtATime.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_stream is null || !_stream.IsConnected)
                throw new InvalidOperationException("agent pipe not connected");

            var bytes = PipeMessageSerializer.Serialize(request);
            await PipeFraming.WriteFrameAsync(_stream, bytes, CancellationToken.None).ConfigureAwait(false);

            var frame = await PipeFraming.ReadFrameAsync(_stream, CancellationToken.None).ConfigureAwait(false)
                ?? throw new InvalidOperationException("agent closed pipe");
            return PipeMessageSerializer.Deserialize(frame);
        }
        finally
        {
            _oneRequestAtATime.Release();
        }
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
