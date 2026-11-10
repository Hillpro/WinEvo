namespace WinEvo.Actions.Abstractions;

/// <summary>
/// Something the user must be told, and confirm, before an action runs. The
/// severity decides how hard the confirmation is.
/// </summary>
public sealed class ActionWarning
{
    public required WarningSeverity Severity { get; init; }

    /// <summary>What the user is told before confirming, in plain language.</summary>
    public required LocalizableText Message { get; init; }
}

public enum WarningSeverity { Info, Warning, Danger, Critical }
