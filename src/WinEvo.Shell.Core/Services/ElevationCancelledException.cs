namespace WinEvo.Shell.Core.Services;

/// <summary>Why an elevated agent launch came back as cancelled.</summary>
public enum ElevationFailureReason
{
    /// <summary>The user dismissed the UAC prompt. The ordinary case.</summary>
    UserDeclined,

    /// <summary>
    /// The agent still carries a download marker that could not be removed, so
    /// Windows most likely blocked the launch before any prompt was shown. See
    /// <see cref="MarkOfTheWeb"/>.
    /// </summary>
    DownloadMarkerPresent,
}

/// <summary>
/// Raised when spawning an elevated agent comes back cancelled. Windows reports
/// a user's UAC decline and a SmartScreen block with the same error code, so
/// <see cref="Reason"/> carries what the Shell was able to infer about which
/// one happened — the caller needs it to tell the user something true.
/// </summary>
public sealed class ElevationCancelledException : Exception
{
    public ElevationCancelledException(
        string message,
        ElevationFailureReason reason = ElevationFailureReason.UserDeclined,
        Exception? inner = null)
        : base(message, inner)
    {
        Reason = reason;
    }

    public ElevationFailureReason Reason { get; }
}
