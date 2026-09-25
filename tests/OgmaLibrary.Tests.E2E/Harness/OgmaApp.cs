using System.Diagnostics;
using System.Management;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace OgmaLibrary.Tests.E2E.Harness;

/// <summary>Per-test isolated folders under <c>%TEMP%\ogma-e2e\&lt;run-id&gt;\&lt;test-id&gt;</c>.</summary>
public sealed class E2ESession : IDisposable
{
    /// <summary>Creates <c>{data,lib}</c> for one test.</summary>
    public E2ESession(string testId)
    {
        Root = Path.Combine(Path.GetTempPath(), "ogma-e2e", E2ESettings.RunId, $"{testId}-{Guid.NewGuid():N}"[..Math.Min(testId.Length + 9, 60)]);
        DataDirectory = Path.Combine(Root, "data");
        LibraryDirectory = Path.Combine(Root, "lib");
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LibraryDirectory);
    }

    /// <summary>The session root.</summary>
    public string Root { get; }

    /// <summary>The isolated <c>OGMA_LIBRARY_DATA_DIR</c>.</summary>
    public string DataDirectory { get; }

    /// <summary>The library folder the journey scans.</summary>
    public string LibraryDirectory { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (E2ESettings.KeepTempData)
        {
            return;
        }

        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }

                return;
            }
            catch (IOException)
            {
                Thread.Sleep(500);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(500);
            }
        }
    }
}

/// <summary>What happened when the harness closed the app.</summary>
public sealed record CloseResult(bool ExitedCleanly, long ExitMilliseconds, IReadOnlyList<int> OrphanedWorkers, int? ExitCode);

/// <summary>
/// The app under test (T01.3): launched from the built exe with an isolated data directory,
/// found through UI Automation, sized with <c>SetWindowPos</c> and closed with <c>WM_CLOSE</c>.
/// Only processes started from the exe under test are ever targeted.
/// </summary>
public sealed class OgmaApp : IDisposable
{
    /// <summary>The command-line marker that identifies harness-launched processes.</summary>
    public const string SessionArgument = "--ogma-e2e-session=";

    private readonly UIA3Automation _automation;
    private bool _closed;

    private OgmaApp(Process process, UIA3Automation automation, Window window, TimeSpan timeToWindow, string exePath)
    {
        Process = process;
        _automation = automation;
        MainWindow = window;
        TimeToWindow = timeToWindow;
        ExePath = exePath;
        Handle = window.Properties.NativeWindowHandle.Value;
    }

    /// <summary>The app process.</summary>
    public Process Process { get; }

    /// <summary>The UIA3 automation instance bound to this app.</summary>
    public UIA3Automation Automation => _automation;

    /// <summary>The main window.</summary>
    public Window MainWindow { get; }

    /// <summary>The main window handle.</summary>
    public nint Handle { get; }

    /// <summary>Launch to first window.</summary>
    public TimeSpan TimeToWindow { get; }

    /// <summary>The exe that was launched.</summary>
    public string ExePath { get; }

    /// <summary>Whether the process is still running.</summary>
    public bool IsAlive
    {
        get
        {
            Process.Refresh();
            return !Process.HasExited;
        }
    }

    /// <summary>The main window as a harness window (for visibility checks).</summary>
    public UiWindow Window => new(_automation, MainWindow, Handle);

    /// <summary>Launches the app for a session and waits (≤ 30 s) for the main window.</summary>
    public static OgmaApp Launch(E2ESession session, IReadOnlyDictionary<string, string>? environment = null)
    {
        string exe = E2ESettings.ExePath;
        if (!File.Exists(exe))
        {
            throw new FileNotFoundException(
                $"App under test not found: {exe}. Build Release first or set OGMA_E2E_EXE (Invoke-GoldenJourneys.ps1 does both).", exe);
        }

        KillStrays(exe);
        var start = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(exe)!,
        };
        start.ArgumentList.Add(SessionArgument + session.DataDirectory);
        start.Environment["OGMA_LIBRARY_DATA_DIR"] = session.DataDirectory;
        start.Environment["OGMA_E2E"] = "1";
        foreach (string name in new[] { "OGMA_E2E_PICK_FOLDER", "OGMA_E2E_INJECT_CRASH", "OGMA_E2E_INJECT_UI_FAULT" })
        {
            start.Environment.Remove(name);
        }

        if (environment is not null)
        {
            foreach ((string key, string value) in environment)
            {
                start.Environment[key] = value;
            }
        }

        var clock = Stopwatch.StartNew();
        Process process = Process.Start(start) ?? throw new InvalidOperationException("Process.Start returned null.");
        var automation = new UIA3Automation
        {
            // First-run migrations run on the UI thread (K71), so the provider can be busy for seconds.
            ConnectionTimeout = TimeSpan.FromSeconds(10),
            TransactionTimeout = TimeSpan.FromSeconds(20),
        };
        try
        {
            nint handle = 0;
            while (clock.Elapsed < TimeSpan.FromSeconds(30))
            {
                process.Refresh();
                if (process.HasExited)
                {
                    throw new InvalidOperationException($"The app exited during startup with code {process.ExitCode}.");
                }

                handle = process.MainWindowHandle;
                if (handle != 0)
                {
                    break;
                }

                Thread.Sleep(100);
            }

            if (handle == 0)
            {
                throw new TimeoutException("The main window did not appear within 30 s.");
            }

            Window window = automation.FromHandle(handle).AsWindow();
            return new OgmaApp(process, automation, window, clock.Elapsed, exe);
        }
        catch
        {
            TryKill(process);
            automation.Dispose();
            throw;
        }
    }

    /// <summary>Moves the window to the top-left corner, sets its outer size and keeps it on top.</summary>
    public void Resize(WindowSize size)
    {
        Native.SetWindowPos(Handle, Native.HwndTopmost, 0, 0, size.Width, size.Height, Native.SwpShowWindow | Native.SwpNoActivate);
        Thread.Sleep(700);
    }

    /// <summary>Worker processes started by this app.</summary>
    public IReadOnlyList<int> WorkerProcessIds() =>
        QueryProcesses($"ParentProcessId = {Process.Id}")
            .Where(p => p.Name.Equals("OgmaLibrary.Workers.exe", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Id)
            .ToArray();

    /// <summary>Closes with <c>WM_CLOSE</c>, waits up to <paramref name="timeout"/>, then kills if needed.</summary>
    public CloseResult Close(TimeSpan? timeout = null)
    {
        if (_closed)
        {
            return new CloseResult(true, 0, [], null);
        }

        _closed = true;
        IReadOnlyList<int> workers = IsAlive ? WorkerProcessIds() : [];
        var clock = Stopwatch.StartNew();
        bool exited = !IsAlive;
        if (!exited)
        {
            Native.PostMessageW(Handle, Native.WmClose, 0, 0);
            exited = Process.WaitForExit((int)(timeout ?? TimeSpan.FromSeconds(10)).TotalMilliseconds);
        }

        long exitMs = clock.ElapsedMilliseconds;
        int? exitCode = exited ? Process.ExitCode : null;
        if (!exited)
        {
            TryKill(Process);
        }

        Thread.Sleep(300);
        var orphans = new List<int>();
        foreach (int id in workers)
        {
            try
            {
                using Process worker = Process.GetProcessById(id);
                if (!worker.HasExited)
                {
                    orphans.Add(id);
                    TryKill(worker);
                }
            }
            catch (ArgumentException)
            {
                // The worker already exited.
            }
        }

        _automation.Dispose();
        return new CloseResult(exited, exitMs, orphans, exitCode);
    }

    /// <summary>Kills the app immediately (used when a test has already failed).</summary>
    public void Kill()
    {
        IReadOnlyList<int> workers = IsAlive ? WorkerProcessIds() : [];
        TryKill(Process);
        foreach (int id in workers)
        {
            try
            {
                using Process worker = Process.GetProcessById(id);
                TryKill(worker);
            }
            catch (ArgumentException)
            {
                // Already gone.
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_closed)
        {
            Close(TimeSpan.FromSeconds(10));
        }

        Process.Dispose();
    }

    /// <summary>
    /// Kills leftovers from earlier E2E runs of the same exe: apps carrying the session marker,
    /// and workers from the exe directory whose parent app no longer exists.
    /// </summary>
    public static int KillStrays(string exePath)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(exePath))!;
        int killed = 0;
        IReadOnlyList<ProcessRow> rows = QueryProcesses("Name = 'OgmaLibrary.App.exe' OR Name = 'OgmaLibrary.Workers.exe'");
        var alive = rows.Select(r => r.Id).ToHashSet();
        foreach (ProcessRow row in rows)
        {
            bool sameExeDirectory = row.ExecutablePath is not null &&
                string.Equals(Path.GetDirectoryName(row.ExecutablePath), directory, StringComparison.OrdinalIgnoreCase);
            if (!sameExeDirectory)
            {
                continue;
            }

            bool strayApp = row.Name.Equals("OgmaLibrary.App.exe", StringComparison.OrdinalIgnoreCase) &&
                row.CommandLine?.Contains(SessionArgument, StringComparison.Ordinal) == true;
            bool orphanWorker = row.Name.Equals("OgmaLibrary.Workers.exe", StringComparison.OrdinalIgnoreCase) &&
                !alive.Contains(row.ParentId) && !IsRunning(row.ParentId);
            if (strayApp || orphanWorker)
            {
                try
                {
                    using Process process = Process.GetProcessById(row.Id);
                    TryKill(process);
                    killed++;
                }
                catch (ArgumentException)
                {
                    // Exited meanwhile.
                }
            }
        }

        return killed;
    }

    /// <summary>Harness-launched app and worker processes from the exe directory still running.</summary>
    public static IReadOnlyList<int> RunningFromExeDirectory(string exePath)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(exePath))!;
        return QueryProcesses("Name = 'OgmaLibrary.App.exe' OR Name = 'OgmaLibrary.Workers.exe'")
            .Where(r => r.ExecutablePath is not null &&
                        string.Equals(Path.GetDirectoryName(r.ExecutablePath), directory, StringComparison.OrdinalIgnoreCase))
            .Where(r => r.Name.Equals("OgmaLibrary.Workers.exe", StringComparison.OrdinalIgnoreCase) ||
                        r.CommandLine?.Contains(SessionArgument, StringComparison.Ordinal) == true)
            .Select(r => r.Id)
            .ToArray();
    }

    private static bool IsRunning(int id)
    {
        try
        {
            using Process process = Process.GetProcessById(id);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Access denied or exiting; nothing more the harness can do.
        }
    }

    private sealed record ProcessRow(int Id, int ParentId, string Name, string? ExecutablePath, string? CommandLine);

    private static List<ProcessRow> QueryProcesses(string where)
    {
        var rows = new List<ProcessRow>();
        using var searcher = new ManagementObjectSearcher(
            $"SELECT ProcessId, ParentProcessId, Name, ExecutablePath, CommandLine FROM Win32_Process WHERE {where}");
        using ManagementObjectCollection results = searcher.Get();
        foreach (ManagementBaseObject item in results)
        {
            using (item)
            {
                rows.Add(new ProcessRow(
                    Convert.ToInt32(item["ProcessId"], System.Globalization.CultureInfo.InvariantCulture),
                    Convert.ToInt32(item["ParentProcessId"], System.Globalization.CultureInfo.InvariantCulture),
                    (string)item["Name"],
                    item["ExecutablePath"] as string,
                    item["CommandLine"] as string));
            }
        }

        return rows;
    }
}
