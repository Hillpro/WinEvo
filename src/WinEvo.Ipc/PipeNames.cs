using System.Diagnostics;

namespace WinEvo.Ipc;

/// <summary>Well-known named-pipe identifiers for agent IPC.</summary>
public static class PipeNames
{
    /// <summary>
    /// Broker mode, per-session pipe. The session-id suffix isolates pipes
    /// across concurrent user sessions (RDP, fast-user-switch). Both Shell
    /// and Agent run in the user's session, so <see cref="Process.GetCurrentProcess"/>
    /// resolves to the same id on both sides.
    /// </summary>
    public static string UserBroker { get; } = $"WinEvo.Agent.User.{Process.GetCurrentProcess().SessionId}";

    /// <summary>Service mode, system-wide pipe. TODO: wire up when service mode is implemented.</summary>
    public const string SystemService = "WinEvo.Agent.System";
}
