using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace OgmaLibrary.App.Views.Catalogue;

/// <summary>
/// A small reading-progress ring (Sept-23 Phase 09): a track circle with an arc for the
/// percentage read. Colours are design tokens (<c>Brush.Accent.Glow</c> on
/// <c>Brush.Rail.Background</c>); the percentage is also exposed as text by the host.
/// </summary>
public sealed class ProgressRing : Control
{
    /// <summary>The progress in percent (0–100).</summary>
    public static readonly StyledProperty<double?> PercentProperty =
        AvaloniaProperty.Register<ProgressRing, double?>(nameof(Percent));

    static ProgressRing()
    {
        AffectsRender<ProgressRing>(PercentProperty);
    }

    /// <summary>The progress in percent (0–100); null or 0 draws nothing.</summary>
    public double? Percent
    {
        get => GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        double percent = Math.Clamp(Percent ?? 0, 0, 100);
        double size = Math.Min(Bounds.Width, Bounds.Height);
        if (percent <= 0 || size < 8)
        {
            return;
        }

        IBrush track = Resolve("Brush.Rail.Background");
        IBrush arc = Resolve("Brush.Accent.Glow");
        const double stroke = 3;
        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);
        double radius = (size - stroke) / 2;
        context.DrawEllipse(track, new Pen(track, stroke), centre, radius, radius);

        double sweep = percent / 100 * 360;
        var pen = new Pen(arc, stroke, lineCap: PenLineCap.Round);
        if (sweep >= 359.9)
        {
            context.DrawEllipse(null, pen, centre, radius, radius);
            return;
        }

        var geometry = new StreamGeometry();
        using (StreamGeometryContext g = geometry.Open())
        {
            Point start = PointOn(centre, radius, 0);
            g.BeginFigure(start, false);
            g.ArcTo(PointOn(centre, radius, sweep), new Size(radius, radius), 0, sweep > 180, SweepDirection.Clockwise);
            g.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    private static Point PointOn(Point centre, double radius, double degrees)
    {
        double radians = (degrees - 90) * Math.PI / 180;
        return new Point(centre.X + (radius * Math.Cos(radians)), centre.Y + (radius * Math.Sin(radians)));
    }

    private IBrush Resolve(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out object? value) && value is IBrush brush
            ? brush
            : Brushes.Gray;
}
