using System.ComponentModel;
using System.Diagnostics;

namespace OgmaLibrary.App.Infrastructure;

/// <summary>Opens an app-owned folder (data, logs) in the platform file manager (Sept-23 Phase 08).</summary>
public static class FolderLauncher
{
    /// <summary>Opens the folder, creating it first when it is missing.</summary>
    /// <param name="path">An absolute folder path owned by Ogma.</param>
    /// <returns>True when the file manager was started.</returns>
    public static bool TryOpen(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return false;
        }

        var start = new ProcessStartInfo { UseShellExecute = false };
        if (OperatingSystem.IsWindows())
        {
            start.FileName = "explorer.exe";
        }
        else if (OperatingSystem.IsMacOS())
        {
            start.FileName = "open";
        }
        else
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(path);
            start.ArgumentList.Add(path);
            using Process? process = Process.Start(start);
            return process is not null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception)
        {
            // The caller shows a localised message; the path stays visible on screen.
            return false;
        }
    }
}
