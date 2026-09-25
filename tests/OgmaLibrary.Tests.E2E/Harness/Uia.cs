using System.Diagnostics;
using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace OgmaLibrary.Tests.E2E.Harness;

/// <summary>A top-level window under test: its automation root and native handle.</summary>
public sealed record UiWindow(UIA3Automation Automation, AutomationElement Root, nint Handle)
{
    /// <summary>The window's client area in screen coordinates.</summary>
    public Rectangle ClientRectangle
    {
        get
        {
            Native.GetClientRect(Handle, out Native.Rect client);
            var origin = new Native.Point();
            Native.ClientToScreen(Handle, ref origin);
            return new Rectangle(origin.X, origin.Y, client.Right - client.Left, client.Bottom - client.Top);
        }
    }

    /// <summary>The outer window rectangle in screen coordinates.</summary>
    public Rectangle WindowRectangle
    {
        get
        {
            Native.GetWindowRect(Handle, out Native.Rect rect);
            return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }
    }
}

/// <summary>AutomationId-first locators and condition waits (retries wait, never assertions).</summary>
public static class Uia
{
    /// <summary>The default wait for a control to appear.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Finds a descendant by AutomationId now, or null.</summary>
    public static AutomationElement? TryFind(AutomationElement root, string automationId) =>
        root.FindFirstDescendant(cf => cf.ByAutomationId(automationId));

    /// <summary>Waits for a descendant with the AutomationId; fails with a clear message on timeout.</summary>
    public static AutomationElement WaitFor(AutomationElement root, string automationId, TimeSpan? timeout = null) =>
        WaitUntil(() => TryFind(root, automationId), timeout ?? DefaultTimeout, $"control '{automationId}'");

    /// <summary>Polls <paramref name="probe"/> until it returns a value.</summary>
    public static T WaitUntil<T>(Func<T?> probe, TimeSpan timeout, string what, int intervalMs = 150)
        where T : class
    {
        var clock = Stopwatch.StartNew();
        Exception? last = null;
        while (true)
        {
            try
            {
                if (probe() is { } value)
                {
                    return value;
                }
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                last = ex;
            }

            if (clock.Elapsed > timeout)
            {
                throw new TimeoutException($"Timed out after {timeout.TotalSeconds:0.#} s waiting for {what}." +
                                           (last is null ? string.Empty : $" Last error: {last.GetType().Name}: {last.Message}"));
            }

            Thread.Sleep(intervalMs);
        }
    }

    /// <summary>Polls a condition until true or the timeout; returns whether it became true.</summary>
    public static bool Poll(Func<bool> condition, TimeSpan timeout, int intervalMs = 150)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed <= timeout)
        {
            try
            {
                if (condition())
                {
                    return true;
                }
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                // The tree changed under the probe, or the app was busy; try again.
            }

            Thread.Sleep(intervalMs);
        }

        return false;
    }

    /// <summary>
    /// UIA failures that mean "not yet" rather than "wrong": the tree changed, the element went
    /// away, or the provider was busy (FlaUI reports a busy UI thread as a TimeoutException).
    /// Waits retry these; assertions never do.
    /// </summary>
    public static bool IsTransient(Exception ex) =>
        ex is System.Runtime.InteropServices.COMException or TimeoutException or InvalidOperationException
            or FlaUI.Core.Exceptions.PropertyNotSupportedException
            or FlaUI.Core.Exceptions.ElementNotAvailableException;

    /// <summary>Activates a control through its UIA pattern (Invoke, Toggle, SelectionItem, ExpandCollapse).</summary>
    public static void Activate(AutomationElement element)
    {
        if (element.Patterns.Invoke.TryGetPattern(out var invoke))
        {
            invoke.Invoke();
        }
        else if (element.Patterns.Toggle.TryGetPattern(out var toggle))
        {
            toggle.Toggle();
        }
        else if (element.Patterns.SelectionItem.TryGetPattern(out var selection))
        {
            selection.Select();
        }
        else if (element.Patterns.ExpandCollapse.TryGetPattern(out var expand))
        {
            expand.Expand();
        }
        else
        {
            throw new InvalidOperationException($"'{Describe(element)}' supports no activation pattern.");
        }
    }

    /// <summary>Sets a text value through the Value pattern.</summary>
    public static void SetText(AutomationElement element, string text)
    {
        if (!element.Patterns.Value.TryGetPattern(out var value))
        {
            throw new InvalidOperationException($"'{Describe(element)}' has no Value pattern.");
        }

        value.SetValue(text);
    }

    /// <summary>Reads a Value pattern value, or the name.</summary>
    public static string? GetText(AutomationElement element) =>
        element.Patterns.Value.TryGetPattern(out var value) ? value.Value.Value : element.Properties.Name.ValueOrDefault;

    /// <summary>List items directly or indirectly under a container.</summary>
    public static AutomationElement[] ListItems(AutomationElement container) =>
        container.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));

    /// <summary>Whether <paramref name="candidate"/> is <paramref name="ancestor"/> or inside it.</summary>
    public static bool IsSameOrDescendant(UIA3Automation automation, AutomationElement candidate, AutomationElement ancestor)
    {
        var walker = automation.TreeWalkerFactory.GetRawViewWalker();
        AutomationElement? current = candidate;
        for (int depth = 0; current is not null && depth < 64; depth++)
        {
            if (automation.Compare(current, ancestor))
            {
                return true;
            }

            current = walker.GetParent(current);
        }

        return false;
    }

    /// <summary>A short diagnostic description of an element.</summary>
    public static string Describe(AutomationElement element)
    {
        try
        {
            string id = element.Properties.AutomationId.ValueOrDefault ?? string.Empty;
            string name = element.Properties.Name.ValueOrDefault ?? string.Empty;
            if (name.Length > 60)
            {
                name = name[..60] + "…";
            }

            return $"{element.Properties.ControlType.ValueOrDefault} id='{id}' name='{name}'";
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException
                                       or FlaUI.Core.Exceptions.ElementNotAvailableException)
        {
            return "(element no longer available)";
        }
    }
}
