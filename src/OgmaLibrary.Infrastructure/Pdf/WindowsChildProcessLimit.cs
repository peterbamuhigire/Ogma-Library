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

    /// <summary>
    /// Creates a Job Object, applies the containment limits and assigns the process.
    /// </summary>
    /// <param name="process">The started worker process.</param>
    /// <param name="maxMemoryBytes">The per-process committed memory ceiling.</param>
    /// <param name="cpuTimeLimit">
    /// The cumulative per-process user CPU limit for one-shot jobs, or <see langword="null"/>
    /// for an interactive reader session. Reader sessions are protected by a per-request
    /// wall-clock watchdog and an idle timeout instead, because a cumulative CPU cap kills
    /// a healthy long reading session (Sept-23 Kaizen K30). Memory, active-process and
    /// kill-on-close limits always apply.
    /// </param>
    public static WindowsChildProcessLimit? TryAssign(
        Process process,
        long maxMemoryBytes,
        TimeSpan? cpuTimeLimit)
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
                             (cpuTimeLimit is null ? 0u : JobObjectLimitProcessTime) |
                             JobObjectLimitProcessMemory,
                ActiveProcessLimit = 1,
                PerProcessUserTimeLimit = cpuTimeLimit?.Ticks ?? 0,
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

    /// <summary>Reads back the limits the operating system applied to this Job Object.</summary>
    /// <returns>The applied limit flags and values, or <see langword="null"/> when unavailable.</returns>
    public JobLimitSnapshot? QueryLimits()
    {
        if (!OperatingSystem.IsWindows() || _handle.IsInvalid || _handle.IsClosed)
        {
            return null;
        }

        int length = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
        IntPtr infoPtr = Marshal.AllocHGlobal(length);
        try
        {
            if (!QueryInformationJobObject(
                    _handle,
                    JobObjectExtendedLimitInformationClass,
                    infoPtr,
                    (uint)length,
                    out _))
            {
                return null;
            }

            JobObjectExtendedLimitInformation info = Marshal.PtrToStructure<JobObjectExtendedLimitInformation>(infoPtr);
            uint flags = info.BasicLimitInformation.LimitFlags;
            return new JobLimitSnapshot(
                KillOnJobClose: (flags & JobObjectLimitKillOnJobClose) != 0,
                ActiveProcessLimit: (flags & JobObjectLimitActiveProcess) != 0
                    ? (int)info.BasicLimitInformation.ActiveProcessLimit
                    : null,
                ProcessMemoryLimitBytes: (flags & JobObjectLimitProcessMemory) != 0
                    ? (long)info.ProcessMemoryLimit
                    : null,
                CpuTimeLimit: (flags & JobObjectLimitProcessTime) != 0
                    ? TimeSpan.FromTicks(info.BasicLimitInformation.PerProcessUserTimeLimit)
                    : null);
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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool QueryInformationJobObject(
        SafeFileHandle hJob,
        int jobObjectInfoClass,
        IntPtr lpJobObjectInfo,
        uint cbJobObjectInfoLength,
        out uint lpReturnLength);

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

/// <summary>Limits applied to a worker Job Object, read back from the operating system.</summary>
/// <param name="KillOnJobClose">Whether closing the job handle kills the worker.</param>
/// <param name="ActiveProcessLimit">The active-process ceiling, when set.</param>
/// <param name="ProcessMemoryLimitBytes">The per-process memory ceiling, when set.</param>
/// <param name="CpuTimeLimit">The cumulative per-process CPU ceiling, when set.</param>
internal sealed record JobLimitSnapshot(
    bool KillOnJobClose,
    int? ActiveProcessLimit,
    long? ProcessMemoryLimitBytes,
    TimeSpan? CpuTimeLimit);
