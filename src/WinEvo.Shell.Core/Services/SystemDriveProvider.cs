namespace WinEvo.Shell.Core.Services;

/// <summary>
/// <see cref="IDriveProvider"/> backed by <see cref="DriveInfo.GetDrives"/>.
/// Only ready drives are returned (a CD/DVD drive with no media shows up in
/// <see cref="DriveInfo"/> but throws on size queries).
/// </summary>
public sealed class SystemDriveProvider : IDriveProvider
{
    public IReadOnlyList<DriveOption> Enumerate(IReadOnlyList<string>? allowedTypes)
    {
        var result = new List<DriveOption>();
        foreach (var d in DriveInfo.GetDrives())
        {
            if (!IsTypeAllowed(d.DriveType.ToString(), allowedTypes))
                continue;
            if (!d.IsReady)
                continue;

            result.Add(new DriveOption(d.RootDirectory.FullName, FormatLabel(d)));
        }
        return result;
    }

    /// <summary>
    /// Pure predicate: case-insensitive membership test against
    /// <paramref name="allowedTypes"/>. A <see langword="null"/> or empty
    /// allow-list admits every type. Exposed for unit testing.
    /// </summary>
    public static bool IsTypeAllowed(string driveType, IReadOnlyList<string>? allowedTypes)
    {
        if (allowedTypes is null || allowedTypes.Count == 0)
            return true;

        foreach (var a in allowedTypes)
            if (string.Equals(a, driveType, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static string FormatLabel(DriveInfo drive)
    {
        var letter = drive.Name.TrimEnd('\\');
        var label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? drive.DriveType.ToString() : drive.VolumeLabel;
        var freeGb = drive.AvailableFreeSpace / (1024d * 1024d * 1024d);
        return $"{letter}  {label}  ({freeGb:0.#} GB free)";
    }
}
