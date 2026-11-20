using System.Drawing;

namespace OgmaLibrary.Tests.E2E.Harness;

/// <summary>Pure arithmetic behind the reader journey oracles (Phase 12 slice 7), unit-tested without a window.</summary>
public static class ReaderMetrics
{
    /// <summary>Nearest-rank percentile; NaN for no samples.</summary>
    public static double Percentile(IReadOnlyCollection<double> samples, double quantile)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentOutOfRangeException.ThrowIfLessThan(quantile, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quantile, 1);
        if (samples.Count == 0)
        {
            return double.NaN;
        }

        double[] sorted = samples.OrderBy(s => s).ToArray();
        int rank = Math.Max(1, (int)Math.Ceiling(sorted.Length * quantile));
        return sorted[rank - 1];
    }

    /// <summary>The fraction of the window client area covered by the page element (clipped to the client).</summary>
    public static double PageAreaShare(Rectangle page, Rectangle client)
    {
        if (client.Width <= 0 || client.Height <= 0)
        {
            return 0;
        }

        Rectangle visible = Rectangle.Intersect(page, client);
        return visible.IsEmpty ? 0 : (double)visible.Width * visible.Height / ((double)client.Width * client.Height);
    }

    /// <summary>
    /// Variance of the 4-neighbour Laplacian over the luminance of <paramref name="area"/>: higher is sharper.
    /// NaN when the area is smaller than the 3x3 kernel.
    /// </summary>
    public static double LaplacianVariance(Bitmap bitmap, Rectangle area)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        Rectangle r = Rectangle.Intersect(area, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        if (r.Width < 3 || r.Height < 3)
        {
            return double.NaN;
        }

        var lum = new double[r.Width, r.Height];
        for (int x = 0; x < r.Width; x++)
        {
            for (int y = 0; y < r.Height; y++)
            {
                Color c = bitmap.GetPixel(r.X + x, r.Y + y);
                lum[x, y] = (0.2126 * c.R) + (0.7152 * c.G) + (0.0722 * c.B);
            }
        }

        double sum = 0, sumSq = 0;
        long n = 0;
        for (int x = 1; x < r.Width - 1; x++)
        {
            for (int y = 1; y < r.Height - 1; y++)
            {
                double v = lum[x - 1, y] + lum[x + 1, y] + lum[x, y - 1] + lum[x, y + 1] - (4 * lum[x, y]);
                sum += v;
                sumSq += v * v;
                n++;
            }
        }

        double mean = sum / n;
        return Math.Max(0, (sumSq / n) - (mean * mean));
    }

    /// <summary>
    /// Edge crispness independent of glyph size and of how much blank page the area holds: the
    /// Laplacian variance of the area divided by the Laplacian variance of the same area after a
    /// 3x3 box blur. Crisp 1-pixel glyph edges lose most of their Laplacian energy when blurred
    /// (measured 4.8 on a correct render); an already soft edge loses little (2.8 for a bitmap
    /// that was drawn at 75 % resolution and stretched, 2.3 at 50 %). NaN when the area is too small
    /// or blank.
    /// </summary>
    public static double EdgeCrispness(Bitmap bitmap, Rectangle area)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        Rectangle r = Rectangle.Intersect(area, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        if (r.Width < 5 || r.Height < 5)
        {
            return double.NaN;
        }

        var lum = new double[r.Width, r.Height];
        for (int x = 0; x < r.Width; x++)
        {
            for (int y = 0; y < r.Height; y++)
            {
                Color c = bitmap.GetPixel(r.X + x, r.Y + y);
                lum[x, y] = (0.2126 * c.R) + (0.7152 * c.G) + (0.0722 * c.B);
            }
        }

        var blurred = new double[r.Width, r.Height];
        for (int x = 1; x < r.Width - 1; x++)
        {
            for (int y = 1; y < r.Height - 1; y++)
            {
                double total = 0;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        total += lum[x + dx, y + dy];
                    }
                }

                blurred[x, y] = total / 9.0;
            }
        }

        double sharp = InnerLaplacianVariance(lum, 2, r.Width - 2, 2, r.Height - 2);
        double soft = InnerLaplacianVariance(blurred, 2, r.Width - 2, 2, r.Height - 2);
        return soft <= 0 ? double.NaN : sharp / soft;
    }

    private static double InnerLaplacianVariance(double[,] lum, int x0, int x1, int y0, int y1)
    {
        double sum = 0, sumSq = 0;
        long n = 0;
        for (int x = x0; x < x1; x++)
        {
            for (int y = y0; y < y1; y++)
            {
                double v = lum[x - 1, y] + lum[x + 1, y] + lum[x, y - 1] + lum[x, y + 1] - (4 * lum[x, y]);
                sum += v;
                sumSq += v * v;
                n++;
            }
        }

        double mean = sum / n;
        return Math.Max(0, (sumSq / n) - (mean * mean));
    }
}
