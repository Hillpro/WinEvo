namespace WinEvo.Shell.Core.Services;

/// <summary>
/// One selectable drive surfaced by a <c>drive</c>-type parameter picker.
/// <see cref="Root"/> is the value handed to the action
/// (e.g. <c>"C:\"</c>); <see cref="Label"/> is the user-facing display string.
/// </summary>
public sealed record DriveOption(string Root, string Label);

/// <summary>
/// Enumerates drives eligible for a <c>drive</c>-type parameter. Abstracted
/// so tests (and any non-live callers) can substitute a fixed set without
/// touching the host machine.
/// </summary>
public interface IDriveProvider
{
    /// <summary>
    /// Returns the drives that match <paramref name="allowedTypes"/> (the
    /// parameter's <c>AllowedDriveTypes</c>). <see langword="null"/> or an
    /// empty list means no filter — all eligible drives.
    /// </summary>
    IReadOnlyList<DriveOption> Enumerate(IReadOnlyList<string>? allowedTypes);
}
