namespace WinEvo.Agent.Core;

/// <summary>
/// Minimal append-only file logger for the agent process. Writes to
/// <c>%TEMP%\winevo-agent.log</c>. Essential because an elevated agent
/// launched via <c>UseShellExecute=true</c> has no visible console;
/// any startup crash otherwise disappears silently.
/// </summary>
public static class AgentLog
{
    private static readonly string s_path =
        Path.Combine(Path.GetTempPath(), "winevo-agent.log");

    private static readonly object s_gate = new();

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
            // Logging must never crash the agent.
        }
    }

    public static void WriteException(string context, Exception ex)
    {
        Write($"{context}: {ex.GetType().Name}: {ex.Message}");
        Write(ex.ToString());
    }
}
