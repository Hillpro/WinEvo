namespace WinEvo.Contracts;

/// <summary>
/// Minimal append-only file logger shared by the Shell and Agent processes.
/// Each process owns one instance pointed at its own file under a common
/// folder. Writes are best-effort: logging never throws, because a logging
/// failure must never take down the host process (especially the elevated
/// agent, which has no console to fall back to).
/// </summary>
public sealed class FileLog
{
    private readonly Lock _gate = new();

    public FileLog(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }

    public void Write(string message)
    {
        try
        {
            lock (_gate)
            {
                File.AppendAllText(
                    FilePath,
                    $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [pid {Environment.ProcessId}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never crash the host process.
        }
    }

    public void WriteException(string context, Exception ex)
    {
        Write($"{context}: {ex.GetType().Name}: {ex.Message}");
        Write(ex.ToString());
    }

    /// <summary>
    /// Builds a log file under <c>%LOCALAPPDATA%\WinEvo\</c>, creating the
    /// directory if needed. Both processes run as the same user (the Shell at
    /// Medium IL, the broker agent UAC-elevated), so this resolves to the same
    /// folder for both — one place to find every WinEvo log.
    /// </summary>
    public static FileLog InWinEvoDataFolder(string fileName)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinEvo");
        try { Directory.CreateDirectory(dir); } catch { /* best-effort; the first Write surfaces real failures */ }
        return new FileLog(Path.Combine(dir, fileName));
    }
}
