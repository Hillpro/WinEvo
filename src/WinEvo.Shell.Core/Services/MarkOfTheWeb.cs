using System.Runtime.InteropServices;

namespace WinEvo.Shell.Core.Services;

/// <summary>Outcome of a <see cref="MarkOfTheWeb.Clear"/> attempt.</summary>
public enum MarkOfTheWebState
{
    /// <summary>The file carried no download marker — nothing to do.</summary>
    NotPresent,

    /// <summary>The marker was present and has been removed.</summary>
    Cleared,

    /// <summary>The marker is present but could not be removed (permissions, read-only media).</summary>
    ClearFailed,
}

/// <summary>
/// Removes the Mark-of-the-Web from a file the Shell itself ships.
///
/// Windows stamps every file extracted from a downloaded archive with a
/// <c>Zone.Identifier</c> alternate data stream. When the Shell then launches
/// the marked agent through ShellExecute + <c>runas</c>, SmartScreen blocks it;
/// because the launch specifies a hidden window, SmartScreen's own prompt never
/// appears and the caller sees only ERROR_CANCELLED — the same code a genuine
/// UAC decline produces. The result is an app that reports "elevation declined"
/// for a prompt the user was never shown.
///
/// Clearing the marker on our own bundled binary is safe: it was extracted from
/// the same archive as the running Shell, which the user already chose to trust
/// by launching it. This does not touch any file the user did not download as
/// part of WinEvo.
/// </summary>
public static class MarkOfTheWeb
{
    private const string ZoneIdentifierStream = ":Zone.Identifier";
    private const int ErrorFileNotFound = 2;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "DeleteFileW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteFile(string fileName);

    /// <summary>
    /// Remove the download marker from <paramref name="filePath"/> if present.
    /// Never throws — the caller decides what a failure means.
    /// </summary>
    public static MarkOfTheWebState Clear(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // .NET has no API for alternate data streams, so delete the stream by
        // its "file:stream" path. A missing stream reports ERROR_FILE_NOT_FOUND,
        // which is the common (unmarked) case rather than an error. Callers
        // check that the base file exists before getting here, so a missing
        // base file is not a case this needs to separate out.
        if (DeleteFile(filePath + ZoneIdentifierStream))
            return MarkOfTheWebState.Cleared;

        return Marshal.GetLastWin32Error() == ErrorFileNotFound
            ? MarkOfTheWebState.NotPresent
            : MarkOfTheWebState.ClearFailed;
    }
}
