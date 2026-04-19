using System.Runtime.InteropServices;

namespace WinEvo.Agent.Core;

/// <summary>
/// Minimal P/Invoke shim to enable a Windows token privilege in the current
/// process. Admin tokens come with many privileges assigned but disabled;
/// this flips them on so SACL-bearing security descriptors can be accepted
/// by the kernel.
/// </summary>
internal static class Privileges
{
    public const string SeSecurity = "SeSecurityPrivilege";

    private const uint TokenAdjustPrivileges = 0x0020;
    private const uint TokenQuery = 0x0008;
    private const uint SePrivilegeEnabled = 0x00000002;

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "LookupPrivilegeValueW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LookupPrivilegeValue(string? systemName, string name, out LUID luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustTokenPrivileges(
        IntPtr tokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges,
        ref TOKEN_PRIVILEGES newState,
        uint bufferLength,
        IntPtr previousState,
        IntPtr returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public LUID_AND_ATTRIBUTES Privilege;
    }

    /// <summary>
    /// Attempt to enable the named privilege on the current process token.
    /// Returns <see langword="true"/> on success, <see langword="false"/> if
    /// the privilege isn't assigned to the token or the API call fails.
    /// </summary>
    public static bool TryEnable(string privilegeName)
    {
        if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out var token))
            return false;

        try
        {
            if (!LookupPrivilegeValue(null, privilegeName, out var luid))
                return false;

            var tp = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privilege = new LUID_AND_ATTRIBUTES
                {
                    Luid = luid,
                    Attributes = SePrivilegeEnabled,
                },
            };

            if (!AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
                return false;

            // AdjustTokenPrivileges returns TRUE even if the privilege wasn't
            // assigned to the token. GetLastError == ERROR_NOT_ALL_ASSIGNED
            // signals partial failure; only ERROR_SUCCESS means fully applied.
            return Marshal.GetLastWin32Error() == 0;
        }
        finally
        {
            CloseHandle(token);
        }
    }
}
