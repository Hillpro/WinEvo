using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WinEvo.Agent.Core;

/// <summary>
/// Windows Job Object configured with JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE, with
/// the current process assigned to it on construction. Child processes spawned
/// by the current process inherit the job membership automatically (Windows'
/// default behavior), so any long-running external process the agent starts
/// (e.g. cipher via ExternalProcessOperation) is implicitly held by the same
/// job without per-child assignment.
///
/// When the last handle to the job closes — which happens when this process
/// terminates, for any reason (graceful exit, crash, external TerminateProcess) —
/// the kernel terminates every process still in the job. That is the guarantee
/// we actually want: no orphaned children after the agent dies.
///
/// The job handle is intentionally kept alive for the whole process lifetime
/// and never explicitly released. A finalizer is deliberately absent: it would
/// run during a GC pass, close the handle, and kill the children prematurely.
/// Keep one instance pinned in a static field in the agent entry point.
/// </summary>
public sealed class JobObject
{
    // JOBOBJECTINFOCLASS enum value for JobObjectExtendedLimitInformation (= 9).
    private const uint JobObjectExtendedLimitInformation = 9;

    // From WinNT.h: automatic termination of all processes in the job when the
    // last job handle closes. This is the one flag doing the load-bearing work.
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

    private readonly IntPtr _handle;

    public JobObject()
    {
        _handle = CreateJobObjectW(IntPtr.Zero, null);
        if (_handle == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateJobObjectW failed");

        try
        {
            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
                },
            };

            var size = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(info, ptr, false);
                if (!SetInformationJobObject(_handle, JobObjectExtendedLimitInformation, ptr, (uint)size))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "SetInformationJobObject failed");
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            // Nested jobs are supported on Windows 8+; if the agent is launched
            // inside an outer job (rare — VS debugger, some AppContainers), our
            // job becomes nested and still enforces KILL_ON_JOB_CLOSE for our
            // branch of the tree.
            if (!AssignProcessToJobObject(_handle, GetCurrentProcess()))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "AssignProcessToJobObject failed");
        }
        catch
        {
            // Partial-init cleanup. Once construction returns successfully we
            // never touch the handle again for the reasons explained above.
            CloseHandle(_handle);
            throw;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObjectW(IntPtr securityAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(
        IntPtr hJob, uint infoClass, IntPtr info, uint infoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }
}
