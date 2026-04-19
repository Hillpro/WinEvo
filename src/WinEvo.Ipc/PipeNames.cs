namespace WinEvo.Ipc;

/// <summary>Well-known named-pipe identifiers for agent IPC.</summary>
public static class PipeNames
{
    /// <summary>
    /// Broker mode, per-session pipe. Currently uses a single-session name.
    /// TODO: include sessionId once the broker launches with correct pipe ACLs.
    /// </summary>
    public const string UserBroker = "WinEvo.Agent.User";

    /// <summary>Service mode, system-wide pipe. TODO: wire up when service mode is implemented.</summary>
    public const string SystemService = "WinEvo.Agent.System";
}
