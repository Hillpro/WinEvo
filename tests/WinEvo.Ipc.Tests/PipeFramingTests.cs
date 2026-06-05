using System.Buffers.Binary;
using WinEvo.Ipc;

namespace WinEvo.Ipc.Tests;

/// <summary>
/// Exercises the 4-byte big-endian length-prefix framing over in-memory
/// streams: round-trips, the empty/EOF cases, reassembly of payloads split
/// across many reads, and rejection of hostile/garbage frame lengths.
/// </summary>
public class PipeFramingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Round_trips_a_payload()
    {
        var payload = "hello pipe"u8.ToArray();
        using var ms = new MemoryStream();

        await PipeFraming.WriteFrameAsync(ms, payload, Ct);
        ms.Position = 0;
        var read = await PipeFraming.ReadFrameAsync(ms, Ct);

        Assert.Equal(payload, read);
    }

    [Fact]
    public async Task Round_trips_an_empty_payload()
    {
        using var ms = new MemoryStream();

        await PipeFraming.WriteFrameAsync(ms, ReadOnlyMemory<byte>.Empty, Ct);
        ms.Position = 0;
        var read = await PipeFraming.ReadFrameAsync(ms, Ct);

        Assert.NotNull(read);
        Assert.Empty(read);
    }

    [Fact]
    public async Task Reads_two_frames_in_order()
    {
        using var ms = new MemoryStream();
        await PipeFraming.WriteFrameAsync(ms, "first"u8.ToArray(), Ct);
        await PipeFraming.WriteFrameAsync(ms, "second"u8.ToArray(), Ct);
        ms.Position = 0;

        Assert.Equal("first"u8.ToArray(), await PipeFraming.ReadFrameAsync(ms, Ct));
        Assert.Equal("second"u8.ToArray(), await PipeFraming.ReadFrameAsync(ms, Ct));
        Assert.Null(await PipeFraming.ReadFrameAsync(ms, Ct)); // clean EOF after the last frame
    }

    [Fact]
    public async Task Clean_eof_returns_null()
    {
        using var ms = new MemoryStream();
        Assert.Null(await PipeFraming.ReadFrameAsync(ms, Ct));
    }

    [Fact]
    public async Task Reassembles_a_payload_delivered_one_byte_at_a_time()
    {
        var payload = "a slightly longer payload that spans many reads"u8.ToArray();
        using var framed = new MemoryStream();
        await PipeFraming.WriteFrameAsync(framed, payload, Ct);

        using var trickle = new ChunkStream(framed.ToArray(), maxChunk: 1);
        var read = await PipeFraming.ReadFrameAsync(trickle, Ct);

        Assert.Equal(payload, read);
    }

    [Fact]
    public async Task Truncated_header_throws()
    {
        using var ms = new MemoryStream([0x00, 0x00]); // 2 of the 4 header bytes
        await Assert.ThrowsAsync<EndOfStreamException>(() => PipeFraming.ReadFrameAsync(ms, Ct));
    }

    [Fact]
    public async Task Truncated_payload_throws()
    {
        var bytes = new byte[4 + 3];
        BinaryPrimitives.WriteInt32BigEndian(bytes, 10); // claims 10 payload bytes...
        using var ms = new MemoryStream(bytes);          // ...but only 3 follow
        await Assert.ThrowsAsync<EndOfStreamException>(() => PipeFraming.ReadFrameAsync(ms, Ct));
    }

    [Fact]
    public async Task Oversized_length_is_rejected()
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, (16 * 1024 * 1024) + 1);
        using var ms = new MemoryStream(header);
        await Assert.ThrowsAsync<InvalidOperationException>(() => PipeFraming.ReadFrameAsync(ms, Ct));
    }

    [Fact]
    public async Task Negative_length_is_rejected()
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, -1);
        using var ms = new MemoryStream(header);
        await Assert.ThrowsAsync<InvalidOperationException>(() => PipeFraming.ReadFrameAsync(ms, Ct));
    }
}

/// <summary>Read-only stream that hands back at most <c>maxChunk</c> bytes per read, to exercise the ReadExact loop.</summary>
file sealed class ChunkStream(byte[] data, int maxChunk) : Stream
{
    private int _pos;

    public override int Read(byte[] buffer, int offset, int count)
        => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var n = Math.Min(Math.Min(buffer.Length, maxChunk), data.Length - _pos);
        if (n <= 0) return 0;
        data.AsSpan(_pos, n).CopyTo(buffer);
        _pos += n;
        return n;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Read(buffer.Span));

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => data.Length;
    public override long Position { get => _pos; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
