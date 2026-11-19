using System.Diagnostics;

namespace OgmaLibrary.App.Infrastructure;

/// <summary>Opens the platform file manager with one file selected.</summary>
public static class FileManagerReveal
{
    /// <summary>
    /// Opens Explorer (Windows) or Finder (macOS) with <paramref name="path"/> selected. The path is
    /// passed as a single argument, never through a shell.
    /// </summary>
    /// <param name="path">An absolute path to an existing file.</param>
    /// <returns><see langword="true"/> when a file manager was started.</returns>
    public static bool Reveal(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var start = new ProcessStartInfo { UseShellExecute = false };
        if (OperatingSystem.IsWindows())
        {
            start.FileName = "explorer.exe";
            start.ArgumentList.Add("/select," + path);
        }
        else if (OperatingSystem.IsMacOS())
        {
            start.FileName = "open";
            start.ArgumentList.Add("-R");
            start.ArgumentList.Add(path);
        }
        else
        {
            return false;
        }

        using Process? process = Process.Start(start);
        return process is not null;
    }
}
