using System.ComponentModel;
using System.Diagnostics;
using WinEvo.Ipc;

namespace WinEvo.Shell.Core.Services;

/// <summary>
/// Spawns the agent broker as a child process and owns its pipe client.
/// Supports launching as a normal user process (default) or via the UAC
/// <c>runas</c> verb when an action declares <c>elevation: required</c>.
/// Elevation is lazy — the Shell starts unelevated and upgrades on demand
/// via <see cref="EnsureElevatedAsync"/>.
/// </summary>
public sealed class AgentLauncher : IAsyncDisposable
{
    private const int UacCancelledHResult = 1223;
    private static readonly TimeSpan s_connectionTimeout = TimeSpan.FromSeconds(15);

    private readonly string _agentExePath;
    private Process? _process;
    private PipeAgentClient? _client;

    public AgentLauncher(string agentExePath)
    {
        _agentExePath = agentExePath;
    }

    /// <summary>Connected client; <see langword="null"/> before <see cref="StartAsync"/> has run or after disposal.</summary>
    public IAgentClient? Client => _client;

    /// <summary>Whether the current broker was launched with elevation.</summary>
    public bool IsElevated { get; private set; }

    /// <summary>Last handshake response from the broker, cached for UI display.</summary>
    public HandshakeResponse? LastHandshake { get; private set; }

    /// <summary>Raised whenever the broker is (re)started or disposed, so UI can refresh status.</summary>
    public event Action? StateChanged;

    /// <summary>
    /// Launch a fresh broker process and connect to its pipe. Fails cleanly if
    /// the process can't start or the user cancels UAC.
    /// </summary>
    public async Task<IAgentClient> StartAsync(bool elevated = false, CancellationToken ct = default)
    {
        if (!File.Exists(_agentExePath))
            throw new FileNotFoundException($"agent executable not found at '{_agentExePath}'");

        Process? process = null;
        PipeAgentClient? client = null;
        try
        {
            process = StartProcess(elevated);
            client = new PipeAgentClient(PipeNames.UserBroker);
            await ConnectWithRetriesAsync(client, process, ct).ConfigureAwait(false);
            var handshake = await client.HandshakeAsync(ct).ConfigureAwait(false);

            // Commit state only after all three succeeded.
            _process = process;
            _client = client;
            IsElevated = elevated;
            LastHandshake = handshake;
            StateChanged?.Invoke();
            return client;
        }
        catch
        {
            if (client is not null)
                await client.DisposeAsync().ConfigureAwait(false);
            TryKill(process);
            throw;
        }
    }

    /// <summary>
    /// Ensure an elevated broker is running. No-op when the current broker is
    /// already elevated and connected. Otherwise tears down the existing
    /// unelevated broker and launches a fresh elevated one (UAC prompt). If
    /// the user declines UAC, the original unelevated broker is restarted so
    /// non-elevated actions keep working, and <see cref="ElevationCancelledException"/>
    /// is thrown.
    /// </summary>
    public async Task<IAgentClient> EnsureElevatedAsync(CancellationToken ct)
    {
        if (IsElevated && _client is { IsConnected: true })
            return _client;

        await TearDownAsync().ConfigureAwait(false);

        try
        {
            return await StartAsync(elevated: true, ct).ConfigureAwait(false);
        }
        catch (ElevationCancelledException)
        {
            // Best-effort recovery: bring the unelevated broker back so the UI
            // keeps working for non-elevated actions. If recovery also fails,
            // callers will see no connected client and surface that separately.
            try { await StartAsync(elevated: false, ct).ConfigureAwait(false); }
            catch { /* swallowed — original cancel is the primary error */ }
            throw;
        }
    }

    private Process StartProcess(bool elevated)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _agentExePath,
            Arguments = "--broker",
        };

        if (elevated)
        {
            // UseShellExecute is required to trigger a UAC prompt via the runas
            // verb, and it's mutually exclusive with stdout/stderr redirection.
            // Paired with WinExe in the agent csproj to ensure no console window.
            psi.UseShellExecute = true;
            psi.Verb = "runas";
            psi.WindowStyle = ProcessWindowStyle.Hidden;
        }
        else
        {
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
        }

        try
        {
            return Process.Start(psi)
                ?? throw new InvalidOperationException($"Process.Start returned null for '{_agentExePath}'");
        }
        catch (Win32Exception ex) when (elevated && ex.NativeErrorCode == UacCancelledHResult)
        {
            throw new ElevationCancelledException("User declined the elevation prompt.", ex);
        }
    }

    private static async Task ConnectWithRetriesAsync(PipeAgentClient client, Process process, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + s_connectionTimeout;
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await client.ConnectAsync(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
                return;
            }
            catch (TimeoutException ex) { lastError = ex; }
            catch (IOException ex) { lastError = ex; }
            catch (UnauthorizedAccessException ex) { lastError = ex; }

            if (HasExited(process))
                throw new InvalidOperationException($"agent exited prematurely");

            await Task.Delay(200, ct).ConfigureAwait(false);
        }

        throw new TimeoutException($"could not connect to agent pipe within {s_connectionTimeout.TotalSeconds}s ({lastError?.Message})");
    }

    private static bool HasExited(Process process)
    {
        // For elevated children launched by an unelevated parent, HasExited can
        // throw due to limited access. Treat "can't tell" as "still running".
        try { return process.HasExited; }
        catch { return false; }
    }

    private static void TryKill(Process? process)
    {
        if (process is null) return;
        try
        {
            if (!HasExited(process))
                process.Kill(entireProcessTree: true);
        }
        catch { /* best-effort */ }
        finally
        {
            try { process.Dispose(); } catch { /* best-effort */ }
        }
    }

    private async Task TearDownAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
            _client = null;
        }
        try { _process?.Dispose(); } catch { /* best-effort */ }
        _process = null;
        IsElevated = false;
        LastHandshake = null;
        StateChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        await TearDownAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Path to the agent executable in the Shell's deployed layout. The
    /// agent ships under an <c>agent/</c> subfolder so its transitive
    /// dependencies don't collide with the Shell's at publish time.
    /// </summary>
    public static string ResolveDefaultAgentPath()
        => Path.Combine(AppContext.BaseDirectory, "agent", "WinEvo.Agent.exe");
}
