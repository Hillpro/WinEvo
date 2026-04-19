using System.Buffers.Binary;

namespace WinEvo.Ipc;

/// <summary>
/// Length-prefixed (4-byte big-endian) UTF-8 framing for the JSON IPC protocol.
/// TODO: swap in the gRPC transport; the <c>.proto</c> contract already lives
/// in <c>WinEvo.Contracts</c>.
/// </summary>
public static class PipeFraming
{
    private const int MaxPayloadBytes = 16 * 1024 * 1024;

    public static async Task WriteFrameAsync(Stream stream, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (payload.Length > MaxPayloadBytes)
            throw new InvalidOperationException($"payload exceeds {MaxPayloadBytes} bytes");

        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        await stream.WriteAsync(header, ct).ConfigureAwait(false);
        await stream.WriteAsync(payload, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public static async Task<byte[]?> ReadFrameAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[4];
        if (!await ReadExactAsync(stream, header, ct).ConfigureAwait(false))
            return null;

        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length < 0 || length > MaxPayloadBytes)
            throw new InvalidOperationException($"invalid frame length {length}");

        var payload = new byte[length];
        if (!await ReadExactAsync(stream, payload, ct).ConfigureAwait(false))
            return null;

        return payload;
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var chunk = await stream.ReadAsync(buffer.AsMemory(read), ct).ConfigureAwait(false);
            if (chunk == 0)
                return read == 0 ? false : throw new EndOfStreamException("stream closed mid-frame");
            read += chunk;
        }
        return true;
    }
}
