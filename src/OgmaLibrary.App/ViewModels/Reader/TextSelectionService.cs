using Avalonia;
using OgmaLibrary.Domain;

namespace OgmaLibrary.App.ViewModels.Reader;

/// <summary>
/// Maps reader surface selection rectangles into Phase 09 normalized
/// <see cref="AnnotationRegion"/> values.
/// </summary>
public static class TextSelectionService
{
    /// <summary>
    /// Converts a screen-space selection on the rendered page surface into a
    /// normalized region relative to the unrotated PDF page.
    /// </summary>
    public static IReadOnlyList<AnnotationRegion> GetRegionsForSelection(
        int pageIndex,
        Rect selectionRect,
        double basePageWidth,
        double basePageHeight,
        double currentZoom,
        int currentRotation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(basePageWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(basePageHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(currentZoom);

        int rotation = NormalizeRotation(currentRotation);
        double renderedWidth = (rotation is 90 or 270 ? basePageHeight : basePageWidth) * currentZoom;
        double renderedHeight = (rotation is 90 or 270 ? basePageWidth : basePageHeight) * currentZoom;

        double left = Clamp(selectionRect.X / renderedWidth, 0, 1);
        double top = Clamp(selectionRect.Y / renderedHeight, 0, 1);
        double width = Clamp(selectionRect.Width / renderedWidth, 0, 1);
        double height = Clamp(selectionRect.Height / renderedHeight, 0, 1);

        (double unrotatedLeft, double unrotatedTop, double unrotatedWidth, double unrotatedHeight) =
            rotation switch
            {
                90 => (top, 1.0 - left - width, height, width),
                180 => (1.0 - left - width, 1.0 - top - height, width, height),
                270 => (1.0 - top - height, left, height, width),
                _ => (left, top, width, height),
            };

        return
        [
            new AnnotationRegion(
                pageIndex,
                Clamp(unrotatedLeft, 0, 1),
                Clamp(unrotatedTop, 0, 1),
                Clamp(unrotatedWidth, 0, 1),
                Clamp(unrotatedHeight, 0, 1)),
        ];
    }

    private static int NormalizeRotation(int degrees)
    {
        int normalized = degrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private static double Clamp(double value, double min, double max) =>
        Math.Min(Math.Max(value, min), max);
}
