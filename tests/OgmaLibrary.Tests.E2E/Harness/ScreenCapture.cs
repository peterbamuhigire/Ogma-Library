using System.Drawing;
using System.Drawing.Imaging;

namespace OgmaLibrary.Tests.E2E.Harness;

/// <summary>Window capture through <c>PrintWindow(PW_RENDERFULLCONTENT)</c>: works when the window is covered or unfocused.</summary>
public static class ScreenCapture
{
    /// <summary>Captures the whole window (outer rectangle) as a bitmap.</summary>
    public static Bitmap CaptureWindow(nint handle)
    {
        Native.GetWindowRect(handle, out Native.Rect rect);
        int width = Math.Max(1, rect.Right - rect.Left);
        int height = Math.Max(1, rect.Bottom - rect.Top);
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        nint hdc = graphics.GetHdc();
        try
        {
            Native.PrintWindow(handle, hdc, Native.PwRenderFullContent);
        }
        finally
        {
            graphics.ReleaseHdc(hdc);
        }

        return bitmap;
    }

    /// <summary>Captures and saves a PNG; returns the path.</summary>
    public static string SaveWindow(nint handle, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using Bitmap bitmap = CaptureWindow(handle);
        bitmap.Save(path, ImageFormat.Png);
        return path;
    }

    /// <summary>
    /// The share of pixels in <paramref name="area"/> (bitmap coordinates) whose CIE76 ΔE from the
    /// dominant colour exceeds <paramref name="deltaE"/>. Near zero means nothing is painted there.
    /// </summary>
    public static double PaintedFraction(Bitmap bitmap, Rectangle area, double deltaE = 10)
    {
        area.Intersect(new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        if (area.Width <= 0 || area.Height <= 0)
        {
            return 0;
        }

        // Sample at most ~40k pixels for speed.
        int step = Math.Max(1, (int)Math.Sqrt(area.Width * (double)area.Height / 40_000));
        var histogram = new Dictionary<int, int>();
        var samples = new List<Color>();
        for (int y = area.Top; y < area.Bottom; y += step)
        {
            for (int x = area.Left; x < area.Right; x += step)
            {
                Color c = bitmap.GetPixel(x, y);
                samples.Add(c);
                int key = ((c.R >> 3) << 10) | ((c.G >> 3) << 5) | (c.B >> 3);
                histogram[key] = histogram.GetValueOrDefault(key) + 1;
            }
        }

        int dominantKey = histogram.MaxBy(pair => pair.Value).Key;
        Color dominant = Color.FromArgb(((dominantKey >> 10) & 31) << 3 | 4, ((dominantKey >> 5) & 31) << 3 | 4, (dominantKey & 31) << 3 | 4);
        (double l0, double a0, double b0) = ToLab(dominant);
        int differing = 0;
        foreach (Color c in samples)
        {
            (double l, double a, double b) = ToLab(c);
            double distance = Math.Sqrt(((l - l0) * (l - l0)) + ((a - a0) * (a - a0)) + ((b - b0) * (b - b0)));
            if (distance > deltaE)
            {
                differing++;
            }
        }

        return differing / (double)samples.Count;
    }

    private static (double L, double A, double B) ToLab(Color color)
    {
        static double Linear(int channel)
        {
            double c = channel / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        double r = Linear(color.R);
        double g = Linear(color.G);
        double b = Linear(color.B);
        double x = ((0.4124 * r) + (0.3576 * g) + (0.1805 * b)) / 0.95047;
        double y = (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
        double z = ((0.0193 * r) + (0.1192 * g) + (0.9505 * b)) / 1.08883;

        static double F(double t) => t > 0.008856 ? Math.Cbrt(t) : (7.787 * t) + (16.0 / 116);

        double fx = F(x);
        double fy = F(y);
        double fz = F(z);
        return ((116 * fy) - 16, 500 * (fx - fy), 200 * (fy - fz));
    }
}
