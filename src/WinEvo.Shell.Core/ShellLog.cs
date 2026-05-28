using WinEvo.Contracts;

namespace WinEvo.Shell.Core;

/// <summary>
/// Append-only diagnostic log for the Shell process, written to
/// <c>%LOCALAPPDATA%\WinEvo\shell.log</c>. Captures unhandled exceptions and
/// service-level failures so silent issues leave an artifact a user can
/// attach to a bug report.
/// </summary>
public static class ShellLog
{
    private static readonly FileLog s_log = FileLog.InWinEvoDataFolder("shell.log");

    public static string FilePath => s_log.FilePath;

    public static void Write(string message) => s_log.Write(message);

    public static void WriteException(string context, Exception ex) => s_log.WriteException(context, ex);
}
