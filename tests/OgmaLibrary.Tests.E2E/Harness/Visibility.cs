using System.Drawing;
using FlaUI.Core.AutomationElements;

namespace OgmaLibrary.Tests.E2E.Harness;

/// <summary>The measured facts behind a visibility verdict.</summary>
public sealed record VisibilityReport(
    string Element,
    Rectangle Bounds,
    Rectangle Client,
    bool IsOffscreen,
    bool InsideClient,
    IReadOnlyList<string> HitResults,
    int OwnHits,
    int ForeignHits,
    double PaintedFraction,
    IReadOnlyList<string> Failures)
{
    /// <summary>Whether every check passed.</summary>
    public bool Passed => Failures.Count == 0;

    /// <inheritdoc />
    public override string ToString() =>
        $"{Element} bounds={Bounds} client={Client} offscreen={IsOffscreen} hits=[{string.Join("; ", HitResults)}] painted={PaintedFraction:P1}" +
        (Passed ? " PASS" : " FAIL: " + string.Join(" | ", Failures));
}

/// <summary>
/// T01.6: an element UIA reports as present must also be visibly painted — inside the window,
/// not covered by another element and not blank. This is the oracle that K10 escaped.
/// </summary>
public static class Visibility
{
    /// <summary>Default minimum share of pixels that must differ from the dominant colour.</summary>
    public const double DefaultMinimumPainted = 0.02;

    /// <summary>
    /// Measures: (1) non-empty bounds inside the client area and not offscreen; (2) hit tests at the
    /// centre and four inset corners return the element or a descendant, and never an unrelated
    /// element (an ancestor, e.g. transparent padding, is inconclusive); (3) at least
    /// <paramref name="minimumPainted"/> of the pixels differ from the dominant colour by ΔE &gt; 10.
    /// With <paramref name="allowScrolledPartially"/> (content in a scroll viewer, such as a reader
    /// page) only the part inside the client area is checked, and it must be non-empty.
    /// </summary>
    public static VisibilityReport Measure(UiWindow window, AutomationElement element, double minimumPainted = DefaultMinimumPainted, bool allowScrolledPartially = false)
    {
        var failures = new List<string>();
        string description = Uia.Describe(element);
        Rectangle bounds = element.BoundingRectangle;
        Rectangle client = window.ClientRectangle;
        bool offscreen = element.Properties.IsOffscreen.ValueOrDefault;
        bool inside = allowScrolledPartially ? client.IntersectsWith(bounds) : client.Contains(bounds);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            failures.Add("empty bounding rectangle");
        }

        if (offscreen)
        {
            failures.Add("IsOffscreen is true");
        }

        if (!inside)
        {
            failures.Add($"not inside the window client area ({bounds} vs {client})");
        }

        var hits = new List<string>();
        int own = 0;
        int foreign = 0;
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            Rectangle visible = Rectangle.Intersect(bounds, client);
            foreach (Point point in SamplePoints(visible))
            {
                AutomationElement? hit = null;
                try
                {
                    hit = window.Automation.FromPoint(point);
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    // Treated as no hit below.
                }

                if (hit is null)
                {
                    hits.Add($"{point.X},{point.Y}: none");
                    foreign++;
                    continue;
                }

                if (Uia.IsSameOrDescendant(window.Automation, hit, element))
                {
                    own++;
                    hits.Add($"{point.X},{point.Y}: own");
                }
                else if (Uia.IsSameOrDescendant(window.Automation, element, hit))
                {
                    hits.Add($"{point.X},{point.Y}: ancestor {Uia.Describe(hit)}");
                }
                else
                {
                    foreign++;
                    hits.Add($"{point.X},{point.Y}: COVERED by {Uia.Describe(hit)}");
                }
            }

            if (foreign > 0)
            {
                failures.Add($"{foreign} of 5 hit-test points land on another element");
            }

            if (own == 0)
            {
                failures.Add("no hit-test point reaches the element");
            }
        }

        double painted = 0;
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            using Bitmap capture = ScreenCapture.CaptureWindow(window.Handle);
            Rectangle windowRect = window.WindowRectangle;
            Rectangle visiblePart = Rectangle.Intersect(bounds, client);
            var local = new Rectangle(visiblePart.X - windowRect.X, visiblePart.Y - windowRect.Y, visiblePart.Width, visiblePart.Height);
            painted = ScreenCapture.PaintedFraction(capture, local);
            if (painted < minimumPainted)
            {
                failures.Add($"only {painted:P1} of pixels painted (minimum {minimumPainted:P0})");
            }
        }

        return new VisibilityReport(description, bounds, client, offscreen, inside, hits, own, foreign, painted, failures);
    }

    /// <summary>Fails the test unless the element is visibly painted.</summary>
    public static VisibilityReport AssertVisiblyPainted(UiWindow window, AutomationElement element, string what, double minimumPainted = DefaultMinimumPainted)
    {
        VisibilityReport report = Measure(window, element, minimumPainted);
        Assert.True(report.Passed, $"{what} is not visibly painted: {report}");
        return report;
    }

    /// <summary>A primary action must be inside the window, enabled, activatable and hit-testable at its centre.</summary>
    public static void AssertReachable(UiWindow window, AutomationElement element, string what)
    {
        Rectangle bounds = element.BoundingRectangle;
        Rectangle client = window.ClientRectangle;
        Assert.True(client.Contains(bounds), $"{what} is outside the window client area: {bounds} vs {client}.");
        Assert.True(element.IsEnabled, $"{what} is disabled.");
        bool activatable = element.Patterns.Invoke.IsSupported || element.Patterns.Toggle.IsSupported ||
                           element.Patterns.SelectionItem.IsSupported || element.Patterns.ExpandCollapse.IsSupported;
        Assert.True(activatable, $"{what} supports no activation pattern.");
        var centre = new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
        AutomationElement hit = window.Automation.FromPoint(centre);
        Assert.True(
            Uia.IsSameOrDescendant(window.Automation, hit, element),
            $"{what} centre {centre} is covered by {Uia.Describe(hit)}.");
    }

    private static IEnumerable<Point> SamplePoints(Rectangle r)
    {
        int insetX = Math.Max(2, r.Width / 5);
        int insetY = Math.Max(2, r.Height / 5);
        yield return new Point(r.X + (r.Width / 2), r.Y + (r.Height / 2));
        yield return new Point(r.Left + insetX, r.Top + insetY);
        yield return new Point(r.Right - insetX, r.Top + insetY);
        yield return new Point(r.Left + insetX, r.Bottom - insetY);
        yield return new Point(r.Right - insetX, r.Bottom - insetY);
    }
}
