using System.Drawing;
using OgmaLibrary.Tests.E2E.Harness;

namespace OgmaLibrary.Tests.E2E.HarnessTests;

/// <summary>Phase 12 slice 7: the reader journey oracles' arithmetic (p95, page-area share, sharpness).</summary>
public sealed class ReaderMetricsTests
{
    /// <summary>Nearest-rank p95 over 100 samples is the 95th value.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Tag", "Harness")]
    public void Percentile_NearestRank_P95()
    {
        double[] samples = Enumerable.Range(1, 100).Select(i => (double)i).Reverse().ToArray();
        Assert.Equal(95, ReaderMetrics.Percentile(samples, 0.95));
        Assert.Equal(100, ReaderMetrics.Percentile(samples, 1.0));
        Assert.Equal(1, ReaderMetrics.Percentile(samples, 0.0));
        Assert.Equal(7, ReaderMetrics.Percentile([7], 0.95));
        Assert.True(double.IsNaN(ReaderMetrics.Percentile([], 0.95)));
    }

    /// <summary>An out-of-range quantile is rejected.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Tag", "Harness")]
    public void Percentile_RejectsBadQuantile() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ReaderMetrics.Percentile([1, 2], 1.5));

    /// <summary>The share is the page area over the window client area, clipped to the window.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Tag", "Harness")]
    public void PageAreaShare_UsesIntersectionWithClient()
    {
        var client = new Rectangle(0, 0, 1000, 800);
        Assert.Equal(0.5, ReaderMetrics.PageAreaShare(new Rectangle(0, 0, 500, 800), client), 6);
        Assert.Equal(0.25, ReaderMetrics.PageAreaShare(new Rectangle(500, 400, 1000, 1000), client), 6);
        Assert.Equal(0, ReaderMetrics.PageAreaShare(new Rectangle(2000, 0, 10, 10), client));
        Assert.Equal(0, ReaderMetrics.PageAreaShare(new Rectangle(0, 0, 10, 10), Rectangle.Empty));
    }

    /// <summary>A flat image has zero Laplacian variance; edges raise it; blur lowers it.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Tag", "Harness")]
    public void LaplacianVariance_FlatIsZero_SharpBeatsBlurred()
    {
        using var flat = new Bitmap(40, 40);
        using (Graphics g = Graphics.FromImage(flat)) { g.Clear(Color.White); }
        Assert.Equal(0, ReaderMetrics.LaplacianVariance(flat, new Rectangle(0, 0, 40, 40)));

        using Bitmap sharp = Stripes(sharpEdges: true);
        using Bitmap soft = Stripes(sharpEdges: false);
        double s = ReaderMetrics.LaplacianVariance(sharp, new Rectangle(0, 0, 40, 40));
        double b = ReaderMetrics.LaplacianVariance(soft, new Rectangle(0, 0, 40, 40));
        Assert.True(s > b * 2, $"sharp {s} should clearly exceed blurred {b}");
    }

    /// <summary>A region smaller than the 3x3 kernel yields NaN rather than a fake zero.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Tag", "Harness")]
    public void LaplacianVariance_TinyRegion_IsNaN()
    {
        using var bmp = new Bitmap(10, 10);
        Assert.True(double.IsNaN(ReaderMetrics.LaplacianVariance(bmp, new Rectangle(0, 0, 2, 2))));
    }

    private static Bitmap Stripes(bool sharpEdges)
    {
        var bmp = new Bitmap(40, 40);
        for (int x = 0; x < 40; x++)
        {
            int v = sharpEdges
                ? ((x / 4) % 2 == 0 ? 0 : 255)
                : (int)(127.5 + (127.5 * Math.Sin(x * Math.PI / 16)));
            for (int y = 0; y < 40; y++)
            {
                bmp.SetPixel(x, y, Color.FromArgb(v, v, v));
            }
        }

        return bmp;
    }
}
