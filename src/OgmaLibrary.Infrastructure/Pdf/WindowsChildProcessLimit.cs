using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OgmaLibrary.Infrastructure.Pdf;

internal sealed class WindowsChildProcessLimit : IDisposable
{
    private const int JobObjectExtendedLimitInformationClass = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;
    private const uint JobObjectLimitActiveProcess = 0x00000008;
    private const uint JobObjectLimitProcessTime = 0x00000002;
    private const uint JobObjectLimitProcessMemory = 0x00000100;

    private readonly SafeFileHandle _handle;

    private WindowsChildProcessLimit(SafeFileHandle handle)
    {
        _handle = handle;
    }

    public static WindowsChildProcessLimit? TryAssign(
        Process process,
        long maxMemoryBytes,
        TimeSpan cpuTimeLimit)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        SafeFileHandle handle = CreateJobObjectW(IntPtr.Zero, null);
        if (handle.IsInvalid)
        {
            return null;
        }

        var info = new JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation
            {
                LimitFlags = JobObjectLimitKillOnJobClose |
                             JobObjectLimitActiveProcess |
                             JobObjectLimitProcessTime |
                             JobObjectLimitProcessMemory,
                ActiveProcessLimit = 1,
                PerProcessUserTimeLimit = cpuTimeLimit.Ticks,
            },
            ProcessMemoryLimit = (nuint)maxMemoryBytes,
        };

        int length = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
        IntPtr infoPtr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(info, infoPtr, fDeleteOld: false);
            if (!SetInformationJobObject(handle, JobObjectExtendedLimitInformationClass, infoPtr, (uint)length))
            {
                handle.Dispose();
                return null;
            }

            if (!AssignProcessToJobObject(handle, process.Handle))
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                if (error == 5)
                {
                    return null;
                }

                throw new Win32Exception(error);
            }

            return new WindowsChildProcessLimit(handle);
        }
        finally
        {
            Marshal.FreeHGlobal(infoPtr);
        }
    }

    public void Dispose()
    {
        _handle.Dispose();
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateJobObjectW(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(
        SafeFileHandle hJob,
        int jobObjectInfoClass,
        IntPtr lpJobObjectInfo,
        uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }
}
