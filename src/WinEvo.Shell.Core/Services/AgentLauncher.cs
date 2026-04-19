using System.Diagnostics;
using WinEvo.Ipc;

namespace WinEvo.Shell.Core.Services;

/// <summary>
/// Spawns the agent broker as a child process and connects a pipe client.
/// TODO: UAC-elevated launch (<c>runas</c> verb) when an action declares
/// <c>elevation: required</c>; currently the broker runs as the current user.
/// </summary>
public sealed class AgentLauncher : IAsyncDisposable
{
    private readonly string _agentExePath;
    private Process? _process;
    private PipeAgentClient? _client;

    public AgentLauncher(string agentExePath)
    {
        _agentExePath = agentExePath;
    }

    public IAgentClient? Client => _client;

    public async Task<IAgentClient> StartAsync(CancellationToken ct)
    {
        if (!File.Exists(_agentExePath))
            throw new FileNotFoundException($"agent executable not found at '{_agentExePath}'");

        var psi = new ProcessStartInfo
        {
            FileName = _agentExePath,
            Arguments = "--broker",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        _process = Process.Start(psi)
            ?? throw new InvalidOperationException($"failed to start agent process '{_agentExePath}'");

        _client = new PipeAgentClient(PipeNames.UserBroker);

        // Agent needs a moment to open the server pipe; retry with a short budget.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        Exception? lastError = null;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await _client.ConnectAsync(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
                return _client;
            }
            catch (TimeoutException ex) { lastError = ex; }
            catch (IOException ex) { lastError = ex; }

            if (_process.HasExited)
                throw new InvalidOperationException($"agent exited prematurely with code {_process.ExitCode}");

            await Task.Delay(200, ct).ConfigureAwait(false);
        }

        throw new TimeoutException($"could not connect to agent pipe within 15s ({lastError?.Message})");
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
            _client = null;
        }

        if (_process is not null)
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    await _process.WaitForExitAsync().ConfigureAwait(false);
                }
            }
            catch { /* best-effort */ }
            _process.Dispose();
            _process = null;
        }
    }

    private static readonly string[] s_devAgentSubpath =
    {
        "src", "WinEvo.Agent", "bin", "Debug", "net10.0-windows10.0.22000.0",
    };

    /// <summary>
    /// Locates <c>WinEvo.Agent.exe</c> in the Shell's base directory, falling back
    /// to walking up to a sibling project output for "dotnet run" dev scenarios.
    /// </summary>
    public static string ResolveDefaultAgentPath()
    {
        var primary = Path.Combine(AppContext.BaseDirectory, "WinEvo.Agent.exe");
        if (File.Exists(primary))
            return primary;

        return FindInRepo("WinEvo.Agent.exe", s_devAgentSubpath) ?? primary;
    }

    private static string? FindInRepo(string fileName, string[] subpath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, Path.Combine(subpath), fileName);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
