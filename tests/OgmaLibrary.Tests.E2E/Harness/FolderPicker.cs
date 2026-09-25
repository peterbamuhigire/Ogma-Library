using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace OgmaLibrary.Tests.E2E.Harness;

/// <summary>
/// Drives the native Windows folder picker (<c>#32770</c>) the app opens from Choose folder (G1b/G2).
/// The dialog's UIA tree exposes no Select Folder button, so the harness first uses the dialog's
/// Win32 controls (the Folder box, cmb13, and IDOK); if those are missing it falls back to the
/// prototype technique: foreground the dialog, Alt+D, type the path, Enter, then click Select
/// Folder at its standard bottom-right offset.
/// </summary>
public static class FolderPicker
{
    private const int FolderNameComboId = 0x047C;
    private const int OkButtonId = 1;

    /// <summary>Finds an open folder dialog owned by the app process, or null.</summary>
    public static AutomationElement? TryFindDialog(OgmaApp app)
    {
        AutomationElement? owned = app.MainWindow.FindFirstChild(cf => cf.ByClassName("#32770"));
        if (owned is not null)
        {
            return owned;
        }

        return app.Automation.GetDesktop()
            .FindAllChildren(cf => cf.ByClassName("#32770"))
            .FirstOrDefault(d => d.Properties.ProcessId.ValueOrDefault == app.Process.Id);
    }

    /// <summary>Waits for the dialog and picks <paramref name="folder"/>; returns the technique used.</summary>
    public static string Pick(OgmaApp app, string folder, TimeSpan? timeout = null)
    {
        AutomationElement dialog = Uia.WaitUntil(() => TryFindDialog(app), timeout ?? TimeSpan.FromSeconds(15), "the native folder dialog");
        nint handle = dialog.Properties.NativeWindowHandle.Value;
        string technique = TryWin32(handle, folder) ? "win32" : KeyboardAndOffsetClick(handle, folder);
        bool closed = Uia.Poll(() => !Native.IsWindow(handle) || !Native.IsWindowVisible(handle), TimeSpan.FromSeconds(10));
        Assert.True(closed, $"The folder dialog is still open after the '{technique}' technique.");
        return technique;
    }

    private static bool TryWin32(nint dialog, string folder)
    {
        nint combo = Native.GetDlgItem(dialog, FolderNameComboId);
        nint ok = Native.GetDlgItem(dialog, OkButtonId);
        if (combo == 0 || ok == 0)
        {
            return false;
        }

        nint edit = FindDescendant(combo, "Edit");
        if (edit == 0)
        {
            return false;
        }

        Native.SendMessageW(edit, Native.WmSetText, 0, folder);
        Native.ForceForeground(dialog);
        Native.SendMessageW(ok, Native.BmClick, 0, 0);
        return Uia.Poll(() => !Native.IsWindow(dialog) || !Native.IsWindowVisible(dialog), TimeSpan.FromSeconds(4));
    }

    private static string KeyboardAndOffsetClick(nint dialog, string folder)
    {
        Native.ForceForeground(dialog);
        Thread.Sleep(500);
        Assert.True(Native.GetForegroundWindow() == dialog, "Could not bring the folder dialog to the foreground.");
        Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_D);
        Thread.Sleep(400);
        Keyboard.Type(folder);
        Keyboard.Press(VirtualKeyShort.RETURN);
        Thread.Sleep(1500);
        Native.GetWindowRect(dialog, out Native.Rect rect);
        Native.ForceForeground(dialog);
        Mouse.Click(new System.Drawing.Point(rect.Right - 179, rect.Bottom - 39));
        return "keyboard+offset-click";
    }

    private static nint FindDescendant(nint parent, string className)
    {
        var stack = new Stack<nint>();
        stack.Push(parent);
        var clock = Stopwatch.StartNew();
        while (stack.Count > 0 && clock.ElapsedMilliseconds < 2000)
        {
            nint current = stack.Pop();
            nint child = 0;
            while ((child = Native.FindWindowExW(current, child, null, null)) != 0)
            {
                if (string.Equals(Native.ClassNameOf(child), className, StringComparison.Ordinal))
                {
                    return child;
                }

                stack.Push(child);
            }
        }

        return 0;
    }
}
