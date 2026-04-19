namespace WinEvo.Shell.Core.Services;

/// <summary>
/// Raised when the user declines the UAC prompt while the Shell is trying to
/// spawn an elevated agent. The caller should report this to the user and
/// leave the action unexecuted.
/// </summary>
public sealed class ElevationCancelledException : Exception
{
    public ElevationCancelledException(string message, Exception? inner = null)
        : base(message, inner) { }
}
