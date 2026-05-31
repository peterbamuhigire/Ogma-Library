using OgmaLibrary.Domain;

namespace OgmaLibrary.Reader.Annotations;

/// <summary>
/// Converts normalized <see cref="AnnotationRegion"/> coordinates to on-screen pixel
/// rectangles, applying the page rotation matrix so that annotations reload correctly
/// on rotated pages (NFR-OGMA-008, FR-READ-008).
/// </summary>
/// <remarks>
/// <para>
/// Normalized coordinates are stored relative to the <em>un-rotated</em> page dimensions.
/// When rendering, the overlay applies the rotation transform so that the highlight
/// appears at the correct position regardless of how many degrees the page is rotated.
/// </para>
/// <para>
/// Supported rotations: 0°, 90°, 180°, 270° (PDF standard values).
/// </para>
/// </remarks>
public static class AnnotationRenderHelper
{
    /// <summary>
    /// Transforms a normalized <see cref="AnnotationRegion"/> to a pixel rectangle
    /// in the coordinate space of the rendered (possibly rotated) page bitmap.
    /// </summary>
    /// <param name="region">The normalized region to transform.</param>
    /// <param name="renderedWidthPx">The width in pixels of the unrotated rendered page bitmap before viewer zoom.</param>
    /// <param name="renderedHeightPx">The height in pixels of the unrotated rendered page bitmap before viewer zoom.</param>
    /// <param name="rotationDegrees">The page rotation in degrees (0, 90, 180, 270).</param>
    /// <param name="zoomFactor">The additional viewer zoom factor applied to the page bitmap.</param>
    /// <returns>
    /// A <see cref="ScreenRect"/> in pixels relative to the top-left of the rendered page.
    /// </returns>
    public static ScreenRect ToScreenRect(
        AnnotationRegion region,
        double renderedWidthPx,
        double renderedHeightPx,
        int rotationDegrees,
        double zoomFactor = 1.0)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoomFactor);

        // Normalize rotation to 0/90/180/270.
        int rot = ((rotationDegrees % 360) + 360) % 360;

        // Map normalized [0,1] to [0,1] in the rotated coordinate system.
        (double left, double top, double width, double height) = rot switch
        {
            // No rotation: identity transform.
            0 => (region.NormLeft, region.NormTop, region.NormWidth, region.NormHeight),

            // 90° clockwise: x' = 1 - top - height, y' = left
            // Un-rotated (w,h) → rotated (h,w).
            90 => (
                1.0 - region.NormTop - region.NormHeight,
                region.NormLeft,
                region.NormHeight,
                region.NormWidth),

            // 180°: x' = 1 - left - width, y' = 1 - top - height.
            180 => (
                1.0 - region.NormLeft - region.NormWidth,
                1.0 - region.NormTop - region.NormHeight,
                region.NormWidth,
                region.NormHeight),

            // 270° clockwise (= 90° counter-clockwise):
            // x' = top, y' = 1 - left - width
            // Un-rotated (w,h) → rotated (h,w).
            270 => (
                region.NormTop,
                1.0 - region.NormLeft - region.NormWidth,
                region.NormHeight,
                region.NormWidth),

            _ => (region.NormLeft, region.NormTop, region.NormWidth, region.NormHeight),
        };

        // For 90° and 270° the rendered bitmap dimensions are swapped.
        double bitmapWidth = (rot is 90 or 270 ? renderedHeightPx : renderedWidthPx) * zoomFactor;
        double bitmapHeight = (rot is 90 or 270 ? renderedWidthPx : renderedHeightPx) * zoomFactor;

        return new ScreenRect(
            X: left * bitmapWidth,
            Y: top * bitmapHeight,
            Width: width * bitmapWidth,
            Height: height * bitmapHeight);
    }
}

/// <summary>
/// A pixel rectangle in the coordinate system of a rendered page bitmap.
/// </summary>
/// <param name="X">Left edge in pixels.</param>
/// <param name="Y">Top edge in pixels.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
public sealed record ScreenRect(double X, double Y, double Width, double Height);
