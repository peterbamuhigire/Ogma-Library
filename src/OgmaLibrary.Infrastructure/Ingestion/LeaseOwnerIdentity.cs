using System.Diagnostics;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Identifies the process that owns a job lease, so leases of a crashed process can be
/// reclaimed at once instead of after the lease expiry (Sept-23 Phase 06, T06.7, K28).
/// The start time guards against a recycled process id.
/// </summary>
public static class LeaseOwnerIdentity
{
    private static readonly Lazy<long> CurrentStart = new(() => ReadStartTicks(Process.GetCurrentProcess()) ?? 0L);

    /// <summary>The current process id.</summary>
    public static int CurrentPid => Environment.ProcessId;

    /// <summary>The current process start time as UTC ticks.</summary>
    public static long CurrentStartTicks => CurrentStart.Value;

    /// <summary>
    /// Returns whether the owner recorded on a lease is still running. Unknown owners (legacy
    /// rows without a pid) are treated as alive so only expiry can reclaim them.
    /// </summary>
    /// <param name="pid">The recorded owner pid.</param>
    /// <param name="startTicks">The recorded owner start time.</param>
    /// <returns>False only when the owner is known to be gone.</returns>
    public static bool IsAlive(int? pid, long? startTicks)
    {
        if (pid is null or <= 0)
        {
            return true;
        }

        if (pid == CurrentPid)
        {
            return startTicks is null || startTicks == CurrentStartTicks;
        }

        try
        {
            using Process process = Process.GetProcessById(pid.Value);
            if (process.HasExited)
            {
                return false;
            }

            long? actualStart = ReadStartTicks(process);
            return startTicks is null || actualStart is null ||
                   Math.Abs(actualStart.Value - startTicks.Value) < TimeSpan.TicksPerSecond;
        }
        catch (ArgumentException)
        {
            // No process with this id is running.
            return false;
        }
        catch (InvalidOperationException)
        {
            // The process exited while it was being inspected.
            return false;
        }
    }

    private static long? ReadStartTicks(Process process)
    {
        try
        {
            return process.StartTime.ToUniversalTime().Ticks;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Access denied to another user's process: its identity cannot be confirmed.
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
