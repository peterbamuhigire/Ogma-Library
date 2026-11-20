using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using OgmaLibrary.App.Views.Reader;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Sept-23 Phase 12 slice 2: the virtualised continuous layout host (READ-004).</summary>
public sealed class ContinuousPageHostTests
{
    private static (Window Window, ContinuousPageHost Host, List<int> Requested) Show()
    {
        var requested = new List<int>();
        var host = new ContinuousPageHost
        {
            BitmapProvider = (page, _) =>
            {
                lock (requested)
                {
                    requested.Add(page);
                }

                return Task.FromResult<Bitmap?>(new WriteableBitmap(
                    new PixelSize(4, 4), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul));
            },
        };
        var window = new Window { Width = 500, Height = 600, Content = host };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        host.Configure(100, 300, 400, 16);
        host.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return (window, host, requested);
    }

    [AvaloniaFact]
    public void ContinuousHost_RealisesOnlyVisiblePlusTwo()
    {
        (Window window, ContinuousPageHost host, List<int> requested) = Show();

        // Viewport 600 over a 416 stride shows pages 0 and 1; two more each side are realised.
        Assert.Equal([0, 1, 2, 3], host.RealisedPages.OrderBy(p => p));

        host.Offset = new Vector(0, 50 * host.PageStride);
        host.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        int[] realised = [.. host.RealisedPages.OrderBy(p => p)];
        Assert.Equal([48, 49, 50, 51, 52, 53], realised);
        Assert.True(host.RealisedPages.Count <= 8, "Only a handful of pages may be realised at once.");

        // Pages that scrolled away hold no bitmap, and the far page was never rendered.
        Assert.DoesNotContain(0, host.PagesWithBitmaps);
        Assert.DoesNotContain(99, requested);
        window.Close();
    }

    [AvaloniaFact]
    public void ContinuousHost_ReportsCentrePage_AndScrollsToPage()
    {
        (Window window, ContinuousPageHost host, _) = Show();
        var centres = new List<int>();
        host.CentrePageChanged += (_, page) => centres.Add(page);

        host.Offset = new Vector(0, 20 * host.PageStride);
        host.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.NotEmpty(centres);
        Assert.Equal(20, centres[^1]);

        // A programmatic jump must not echo back as a user scroll.
        centres.Clear();
        host.ScrollToPage(60);
        host.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(centres);
        Assert.Contains(60, host.RealisedPages);
        window.Close();
    }
}
