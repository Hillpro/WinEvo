using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace WinEvo.Agent.Core;

/// <summary>
/// Watches a <see cref="NamedPipeServerStream"/> for client disconnect and
/// cancels a caller-supplied token source as soon as the kernel reports the
/// pipe as broken. Exists because <see cref="PipeStream.IsConnected"/> is a
/// managed flag that only refreshes after an I/O operation completes — with
/// <see cref="PipeOptions.Asynchronous"/>, a pending <c>ReadAsync</c> can stay
/// parked after the client disconnects without ever waking up, which would
/// leave the agent alive indefinitely after the Shell is closed.
/// <para/>
/// <see cref="PeekNamedPipe"/> queries the kernel directly and returns
/// <see cref="ERROR_BROKEN_PIPE"/> once the client is gone, without disturbing
/// any pending read on the same handle.
/// </summary>
internal static class PipeConnectionMonitor
{
    private const int ERROR_BROKEN_PIPE = 109;

    /// <summary>
    /// Polls the pipe's kernel state and cancels <paramref name="cts"/> as soon
    /// as the client goes away. Returns when <paramref name="cts"/> is canceled
    /// (by the caller at session end, or by this method on disconnect).
    /// </summary>
    /// <remarks>
    /// 200 ms is a compromise between disconnect latency (user perceives a
    /// ~½ s end-to-end teardown worst case) and wakeup overhead.
    /// </remarks>
    public static async Task WatchAsync(NamedPipeServerStream server, CancellationTokenSource cts)
    {
        try
        {
            while (!cts.IsCancellationRequested)
            {
                if (IsPipeBroken(server))
                {
                    cts.Cancel();
                    return;
                }
                await Task.Delay(200, cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* the session finished; exit normally */ }
    }

    private static bool IsPipeBroken(NamedPipeServerStream server)
    {
        var handle = server.SafePipeHandle;
        if (handle is null || handle.IsInvalid || handle.IsClosed)
            return true;

        var addedRef = false;
        try
        {
            handle.DangerousAddRef(ref addedRef);
            var ok = PeekNamedPipe(
                handle.DangerousGetHandle(),
                IntPtr.Zero, 0,
                out _, out _, out _);
            if (ok) return false;
            var err = Marshal.GetLastWin32Error();
            // ERROR_BROKEN_PIPE when the client has closed its end. Any other
            // error is treated as broken too so we fail safe — we'd rather tear
            // down an agent than leak one.
            return err == ERROR_BROKEN_PIPE || err != 0;
        }
        catch
        {
            return true;
        }
        finally
        {
            if (addedRef) handle.DangerousRelease();
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekNamedPipe(
        IntPtr hNamedPipe,
        IntPtr lpBuffer,
        uint nBufferSize,
        out uint lpBytesRead,
        out uint lpTotalBytesAvail,
        out uint lpBytesLeftThisMessage);
}
