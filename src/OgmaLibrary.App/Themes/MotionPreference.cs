using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace OgmaLibrary.App.Themes;

/// <summary>
/// Decides whether decorative motion (card lift, cross-fades, colour transitions) is allowed
/// (Sept-23 Phase 09). Motion is off when <c>OGMA_REDUCED_MOTION=1</c> is set or, on Windows,
/// when the OS "Show animations in Windows" setting is off. The styles in
/// <c>Themes/Controls.axaml</c> only animate under a window carrying the <c>motion</c> class.
/// </summary>
public static class MotionPreference
{
    /// <summary>The window class that enables transitions.</summary>
    public const string MotionClass = "motion";

    private const uint SpiGetClientAreaAnimation = 0x1042;

    /// <summary>Whether decorative motion is allowed on this machine.</summary>
    /// <returns><see langword="true"/> when transitions may run.</returns>
    public static bool IsMotionAllowed()
    {
        string? overrideValue = Environment.GetEnvironmentVariable("OGMA_REDUCED_MOTION");
        if (overrideValue is "1" or "true" or "TRUE" or "True")
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            try
            {
                return !SystemParametersInfo(SpiGetClientAreaAnimation, 0, out int enabled, 0) || enabled != 0;
            }
            catch (EntryPointNotFoundException)
            {
                return true;
            }
            catch (DllNotFoundException)
            {
                return true;
            }
        }

        return true;
    }

    /// <summary>Adds or removes the <c>motion</c> class on a window.</summary>
    /// <param name="window">The window.</param>
    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.Classes.Set(MotionClass, IsMotionAllowed());
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint param, out int value, uint winIni);
}
