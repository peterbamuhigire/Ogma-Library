using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels.Reader;

namespace OgmaLibrary.App.Views.Reader;

/// <summary>
/// A vertical, virtualised page host for the continuous reader layout (FR-READ-004). Only the
/// pages the viewport touches, plus <see cref="RealiseMargin"/> pages each side, hold a
/// bitmap; pages that scroll away are removed and their bitmaps disposed, so memory stays
/// bounded however long the document is. The host never blocks on the PDF worker: bitmaps
/// arrive asynchronously from <see cref="BitmapProvider"/>.
/// </summary>
public sealed class ContinuousPageHost : ScrollViewer
{
    /// <summary>Pages realised above and below the visible ones.</summary>
    public const int RealiseMargin = 2;

    private readonly Canvas _canvas = new() { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
    private readonly Dictionary<int, Image> _realised = [];
    private readonly Dictionary<int, CancellationTokenSource> _pending = [];
    private int _pageCount;
    private double _pageWidth = 400;
    private double _pageHeight = 566;
    private double _gap = 16;
    private int _centrePage = -1;
    private int _generation;
    private bool _scrollingToPage;

    /// <summary>Creates the host.</summary>
    public ContinuousPageHost()
    {
        Content = _canvas;
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center;
    }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    /// <summary>Supplies the bitmap of a page; the host owns and disposes what it returns.</summary>
    public Func<int, CancellationToken, Task<Bitmap?>>? BitmapProvider { get; set; }

    /// <summary>Raised when the page under the viewport centre changes.</summary>
    public event EventHandler<int>? CentrePageChanged;

    /// <summary>The page indexes that currently hold a realised page element.</summary>
    public IReadOnlyCollection<int> RealisedPages => _realised.Keys;

    /// <summary>The page indexes that currently hold a decoded bitmap.</summary>
    public IReadOnlyCollection<int> PagesWithBitmaps =>
        _realised.Where(pair => pair.Value.Source is not null).Select(pair => pair.Key).ToList();

    /// <summary>The distance between the tops of consecutive pages.</summary>
    public double PageStride => _pageHeight + _gap;

    /// <summary>Sets the document geometry; pages are re-realised at the new size.</summary>
    /// <param name="pageCount">The number of pages.</param>
    /// <param name="pageWidth">The displayed page width.</param>
    /// <param name="pageHeight">The displayed page height.</param>
    /// <param name="gap">The gap between pages.</param>
    public void Configure(int pageCount, double pageWidth, double pageHeight, double gap)
    {
        bool sameShape = pageCount == _pageCount
            && Math.Abs(pageWidth - _pageWidth) < 0.5
            && Math.Abs(pageHeight - _pageHeight) < 0.5
            && Math.Abs(gap - _gap) < 0.5;
        if (sameShape)
        {
            return;
        }

        _pageCount = Math.Max(0, pageCount);
        _pageWidth = Math.Max(1, pageWidth);
        _pageHeight = Math.Max(1, pageHeight);
        _gap = Math.Max(0, gap);
        _generation++;
        _centrePage = -1;
        UnrealiseAll();
        _canvas.Width = _pageWidth;
        _canvas.Height = _pageCount * PageStride;
        Refresh();
    }

    /// <summary>Scrolls so that a page's top is at the top of the viewport.</summary>
    /// <param name="pageIndex">The zero-based page.</param>
    public void ScrollToPage(int pageIndex)
    {
        if (_pageCount == 0)
        {
            return;
        }

        int page = Math.Clamp(pageIndex, 0, _pageCount - 1);
        if (page == _centrePage)
        {
            return;
        }

        _scrollingToPage = true;
        try
        {
            Offset = new Vector(Offset.X, page * PageStride);
            _centrePage = page;
            Refresh();
        }
        finally
        {
            _scrollingToPage = false;
        }
    }

    /// <summary>Realises the pages around the viewport and drops the rest.</summary>
    public void Refresh()
    {
        double viewport = Viewport.Height > 0 ? Viewport.Height : Bounds.Height;
        (int first, int last) = ReaderLayoutPlanner.RealisedRange(
            Offset.Y, viewport, PageStride, _pageCount, RealiseMargin);

        foreach (int page in _realised.Keys.Where(p => p < first || p > last).ToList())
        {
            Unrealise(page);
        }

        for (int page = first; page <= last; page++)
        {
            if (!_realised.ContainsKey(page))
            {
                Realise(page);
            }
        }

        int centre = ReaderLayoutPlanner.CentrePage(Offset.Y, viewport, PageStride, _pageCount);
        if (!_scrollingToPage && centre != _centrePage && _pageCount > 0)
        {
            _centrePage = centre;
            CentrePageChanged?.Invoke(this, centre);
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OffsetProperty || change.Property == ViewportProperty)
        {
            Refresh();
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _generation++;
        UnrealiseAll();
    }

    private void Realise(int page)
    {
        var image = new Image
        {
            Width = _pageWidth,
            Height = _pageHeight,
            Stretch = Stretch.Uniform,
            Tag = page,
        };
        RenderOptions.SetBitmapInterpolationMode(image, Avalonia.Media.Imaging.BitmapInterpolationMode.HighQuality);
        Canvas.SetTop(image, page * PageStride);
        Canvas.SetLeft(image, 0);
        Avalonia.Automation.AutomationProperties.SetAutomationId(image, $"Reader.ContinuousPage.{page + 1}");
        _canvas.Children.Add(image);
        _realised[page] = image;

        if (BitmapProvider is { } provider)
        {
            var cts = new CancellationTokenSource();
            _pending[page] = cts;
            _ = LoadAsync(page, image, provider, cts, _generation);
        }
    }

    private async Task LoadAsync(
        int page, Image image, Func<int, CancellationToken, Task<Bitmap?>> provider, CancellationTokenSource cts, int generation)
    {
        Bitmap? bitmap = null;
        try
        {
            bitmap = await provider(page, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The page scrolled away before it finished.
        }
        catch (ObjectDisposedException)
        {
            // The document closed while the page was rendering.
        }
#pragma warning disable CA1031 // A failed page keeps its blank placeholder; the other pages stay usable.
        catch (Exception)
        {
            // Intentionally ignored: the reader's own render path logs and reports engine failures.
        }
#pragma warning restore CA1031

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (bitmap is null)
            {
                return;
            }

            bool stillWanted = generation == _generation
                && !cts.IsCancellationRequested
                && _realised.TryGetValue(page, out Image? current)
                && ReferenceEquals(current, image);
            if (!stillWanted)
            {
                bitmap.Dispose();
                return;
            }

            image.Source = bitmap;
        });
    }

    private void Unrealise(int page)
    {
        if (_pending.Remove(page, out CancellationTokenSource? cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        if (_realised.Remove(page, out Image? image))
        {
            _canvas.Children.Remove(image);
            Bitmap? bitmap = image.Source as Bitmap;
            image.Source = null;
            bitmap?.Dispose();
        }
    }

    private void UnrealiseAll()
    {
        foreach (int page in _realised.Keys.ToList())
        {
            Unrealise(page);
        }
    }
}
