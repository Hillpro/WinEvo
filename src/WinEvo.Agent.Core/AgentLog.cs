using WinEvo.Contracts;

namespace WinEvo.Agent.Core;

/// <summary>
/// Append-only diagnostic log for the agent process, written to
/// <c>%LOCALAPPDATA%\WinEvo\agent.log</c>. Essential because an elevated
/// agent launched via <c>UseShellExecute=true</c> has no visible console;
/// any startup crash otherwise disappears silently.
/// </summary>
public static class AgentLog
{
    private static readonly FileLog s_log = FileLog.InWinEvoDataFolder("agent.log");

    public static string FilePath => s_log.FilePath;

    public static void Write(string message) => s_log.Write(message);

    public static void WriteException(string context, Exception ex) => s_log.WriteException(context, ex);
}
