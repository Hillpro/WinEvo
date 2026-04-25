namespace WinEvo.Shell.Core;

/// <summary>
/// Minimal append-only file logger for the Shell process. Writes to
/// <c>%LOCALAPPDATA%\WinEvo\shell.log</c>. Mirrors the agent's
/// <c>WinEvo.Agent.Core.AgentLog</c>; captures unhandled exceptions and
/// service-level failures so silent issues leave an artifact a user can
/// attach to a bug report.
/// </summary>
public static class ShellLog
{
    private static readonly string s_path = ResolvePath();
    private static readonly Lock s_gate = new();

    public static string FilePath => s_path;

    public static void Write(string message)
    {
        try
        {
            lock (s_gate)
            {
                File.AppendAllText(
                    s_path,
                    $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [pid {Environment.ProcessId}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never crash the Shell.
        }
    }

    public static void WriteException(string context, Exception ex)
    {
        Write($"{context}: {ex.GetType().Name}: {ex.Message}");
        Write(ex.ToString());
    }

    private static string ResolvePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinEvo");
        try { Directory.CreateDirectory(dir); } catch { /* best-effort; AppendAllText surfaces real failures */ }
        return Path.Combine(dir, "shell.log");
    }
}
