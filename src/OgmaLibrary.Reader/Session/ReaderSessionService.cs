using System.Reactive.Subjects;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Reader.Cache;

namespace OgmaLibrary.Reader.Session;

/// <summary>
/// Manages a single open reader session: opening, resuming, navigating, persisting,
/// and closing a PDF document. Implements FR-READ-001 (open + resume) and feeds the
/// <see cref="IReaderSessionReadModel"/> event stream for LAN-projection readiness.
/// </summary>
/// <remarks>
/// Sept-23 Kaizen Phase 04: navigation never performs synchronous worker IPC, page
/// geometry comes from the renderer's per-document cache, renderer teardown runs off
/// the calling thread, and open/close are serialised so a double open cannot leak a
/// worker.
/// </remarks>
public sealed class ReaderSessionService : IReaderSessionService, IReaderSessionReadModel, IDisposable
{
    /// <summary>Pages kept in flight around the target page when the reader turns pages.</summary>
    internal const int KeepWindow = 2;

    private readonly IPdfRendererFactory _rendererFactory;
    private readonly IReadingProgressService _progressService;
    private readonly IBookFileLocator _fileLocator;
    private readonly PageRenderCache _renderCache;
    private readonly Subject<ReaderEvent> _events = new();
    private readonly Lock _eventsSync = new();
    private readonly SemaphoreSlim _lifecycle = new(1, 1);

    private ReaderSession? _currentSession;
    private IPdfRenderer? _currentRenderer;
    private int _renderWidthPx = ReaderRenderDefaults.PageWidthPx;
    private long _navigationSequence;

    /// <summary>
    /// Initializes a new instance of <see cref="ReaderSessionService"/>.
    /// </summary>
    /// <param name="rendererFactory">Factory for opening PDFium document handles.</param>
    /// <param name="progressService">Service for persisting reading progress.</param>
    /// <param name="fileLocator">Locates the PDF file from a book identity.</param>
    /// <param name="renderCache">The page render cache.</param>
    public ReaderSessionService(
        IPdfRendererFactory rendererFactory,
        IReadingProgressService progressService,
        IBookFileLocator fileLocator,
        PageRenderCache renderCache)
    {
        ArgumentNullException.ThrowIfNull(rendererFactory);
        ArgumentNullException.ThrowIfNull(progressService);
        ArgumentNullException.ThrowIfNull(fileLocator);
        ArgumentNullException.ThrowIfNull(renderCache);

        _rendererFactory = rendererFactory;
        _progressService = progressService;
        _fileLocator = fileLocator;
        _renderCache = renderCache;
    }

    /// <inheritdoc />
    public ReaderSession? CurrentSession => Volatile.Read(ref _currentSession);

    /// <inheritdoc />
    public IPdfRenderer? CurrentRenderer => Volatile.Read(ref _currentRenderer);

    /// <inheritdoc />
    public IObservable<ReaderEvent> Events => _events;

    /// <summary>Gets the render width, in pixels, used for prefetching neighbour pages.</summary>
    public int RenderWidthPx => Volatile.Read(ref _renderWidthPx);

    /// <inheritdoc />
    public Task<ReaderSession> OpenAsync(string bookId, int? pageHint, CancellationToken ct) =>
        OpenCoreAsync(bookId, pageHint, password: null, ct);

    /// <inheritdoc />
    public Task<ReaderSession> OpenProtectedAsync(
        string bookId,
        int? pageHint,
        char[] password,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(password);
        return OpenCoreAsync(bookId, pageHint, password, ct);
    }

    private async Task<ReaderSession> OpenCoreAsync(
        string bookId,
        int? pageHint,
        char[]? password,
        CancellationToken ct)
    {
        // Serialise open/close: two racing opens must not both create workers and
        // leave one orphaned (T04.7).
        await _lifecycle.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_currentSession is not null)
            {
                await CloseCoreAsync(ct).ConfigureAwait(false);
            }

            // Locate the file.
            string? filePath = await _fileLocator.LocateAsync(bookId, ct).ConfigureAwait(false);
            if (filePath is null)
            {
                throw new FileNotFoundException($"No available PDF file found for book '{bookId}'.");
            }

            // Load saved progress (FR-READ-001 resume).
            ReaderProgress progress = await _progressService.LoadAsync(bookId, ct).ConfigureAwait(false);

            // Renderer construction starts the isolated worker, copies and parses the PDF.
            // Keep it off the UI thread even though the factory API is synchronous.
            IPdfRenderer renderer = await Task.Run(
                () => password is null
                    ? _rendererFactory.Open(filePath)
                    : _rendererFactory.Open(filePath, password),
                ct).ConfigureAwait(false);

            int startPage;
            int rotation;
            try
            {
                startPage = pageHint ?? progress.LastPageIndex;
                startPage = Math.Clamp(startPage, 0, Math.Max(0, renderer.PageCount - 1));
                rotation = renderer.PageCount > 0
                    ? await GetRotationAsync(renderer, startPage, fallback: 0, ct).ConfigureAwait(false)
                    : 0;
            }
            catch
            {
                await DisposeRendererAsync(renderer).ConfigureAwait(false);
                throw;
            }

            Volatile.Write(ref _currentRenderer, renderer);
            if (renderer is IPdfRendererHealth health)
            {
                health.Recovered += OnRendererRecovered;
            }

            // Register renderer with the cache.
            _renderCache.SetRenderer(bookId, renderer);

            var session = new ReaderSession(
                bookId,
                filePath,
                renderer.PageCount,
                startPage,
                progress.ScrollOffset,
                progress.ZoomMode,
                progress.ZoomPercent,
                progress.DisplayMode,
                rotation);

            Volatile.Write(ref _currentSession, session);

            // Kick off pre-renders for first page and its neighbour.
            TriggerPrefetch(session);

            Publish(new ReaderEvent.SessionOpened(bookId));
            Publish(new ReaderEvent.PageChanged(bookId, startPage, renderer.PageCount));

            return session;
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    /// <inheritdoc />
    public async Task CloseAsync(CancellationToken ct)
    {
        await _lifecycle.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await CloseCoreAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    private async Task CloseCoreAsync(CancellationToken ct)
    {
        ReaderSession? session = _currentSession;
        if (session is null)
        {
            return;
        }

        string bookId = session.BookId;
        Interlocked.Increment(ref _navigationSequence);

        // Flush progress for durability (NFR-OGMA-008).
        var progress = new ReaderProgress(
            session.CurrentPageIndex,
            session.ScrollOffset,
            session.ZoomMode,
            session.ZoomPercent,
            session.DisplayMode);

        try
        {
            await _progressService.SaveImmediateAsync(bookId, progress, ct).ConfigureAwait(false);
        }
        finally
        {
            IPdfRenderer? renderer = Interlocked.Exchange(ref _currentRenderer, null);
            Volatile.Write(ref _currentSession, null);
            if (renderer is IPdfRendererHealth health)
            {
                health.Recovered -= OnRendererRecovered;
            }

            // Cancelling pending renders and stopping the worker waits (bounded) for the
            // worker to exit; never do that on the caller's (UI) thread.
            await Task.Run(
                    () =>
                    {
                        _renderCache.Invalidate(bookId);
                        renderer?.Dispose();
                    },
                    CancellationToken.None)
                .ConfigureAwait(false);

            Publish(new ReaderEvent.SessionClosed(bookId));
        }
    }

    /// <inheritdoc />
    public async Task NavigateToAsync(int pageIndex, double scrollOffset = 0.0)
    {
        ReaderSession? session = _currentSession;
        IPdfRenderer? renderer = _currentRenderer;
        if (session is null)
        {
            return;
        }

        long sequence = Interlocked.Increment(ref _navigationSequence);
        pageIndex = Math.Clamp(pageIndex, 0, Math.Max(0, session.PageCount - 1));

        // Geometry is cached per document by the renderer, so after the first visit this
        // completes synchronously; the first visit is awaited, never blocked on (K32).
        int rotation = await GetRotationAsync(
                renderer,
                pageIndex,
                fallback: session.PageRotationDegrees,
                CancellationToken.None)
            .ConfigureAwait(false);

        // A newer navigation or a close superseded this one while geometry was loading.
        if (sequence != Interlocked.Read(ref _navigationSequence) ||
            !ReferenceEquals(renderer, _currentRenderer) ||
            _currentSession is not { } current ||
            !string.Equals(current.BookId, session.BookId, StringComparison.Ordinal))
        {
            return;
        }

        current = current.WithPage(pageIndex, scrollOffset, rotation);
        Volatile.Write(ref _currentSession, current);

        // Cancel queued renders that are no longer near the target page.
        _renderCache.CancelOutside(current.BookId, GetKeepRange(current));

        TriggerPrefetch(current);

        Publish(new ReaderEvent.PageChanged(current.BookId, pageIndex, current.PageCount));

        // Debounce progress write (500 ms).
        var progress = new ReaderProgress(
            pageIndex,
            scrollOffset,
            current.ZoomMode,
            current.ZoomPercent,
            current.DisplayMode);
        _progressService.ScheduleSave(current.BookId, progress);
    }

    /// <inheritdoc />
    public void UpdateRenderWidth(int widthPx)
    {
        int bounded = Math.Clamp(widthPx, ReaderRenderDefaults.MinPageWidthPx, ReaderRenderDefaults.MaxPageWidthPx);
        if (Interlocked.Exchange(ref _renderWidthPx, bounded) == bounded)
        {
            return;
        }

        if (_currentSession is { } session)
        {
            // Neighbours rendered at the old width are useless now; warm the new bucket.
            TriggerPrefetch(session);
        }
    }

    /// <inheritdoc />
    public void UpdateScrollOffset(double scrollOffset)
    {
        if (_currentSession is null)
        {
            return;
        }

        ReaderSession session = _currentSession with { ScrollOffset = scrollOffset };
        Volatile.Write(ref _currentSession, session);

        Publish(new ReaderEvent.Scrolled(session.BookId, scrollOffset));

        var progress = new ReaderProgress(
            session.CurrentPageIndex,
            scrollOffset,
            session.ZoomMode,
            session.ZoomPercent,
            session.DisplayMode);
        _progressService.ScheduleSave(session.BookId, progress);
    }

    /// <summary>Updates zoom mode and triggers a progress save.</summary>
    /// <param name="mode">The new zoom mode.</param>
    /// <param name="percent">The effective zoom percent.</param>
    public void UpdateZoom(ZoomMode mode, double percent)
    {
        if (_currentSession is null)
        {
            return;
        }

        ReaderSession session = _currentSession.WithZoom(mode, percent);
        Volatile.Write(ref _currentSession, session);

        Publish(new ReaderEvent.ZoomChanged(session.BookId, mode, percent));

        var progress = new ReaderProgress(
            session.CurrentPageIndex,
            session.ScrollOffset,
            mode,
            percent,
            session.DisplayMode);
        _progressService.ScheduleSave(session.BookId, progress);
    }

    /// <summary>Updates display mode and triggers a progress save.</summary>
    /// <param name="mode">The new display mode.</param>
    public void UpdateDisplayMode(DisplayMode mode)
    {
        if (_currentSession is null)
        {
            return;
        }

        ReaderSession session = _currentSession.WithDisplayMode(mode);
        Volatile.Write(ref _currentSession, session);

        Publish(new ReaderEvent.DisplayModeChanged(session.BookId, mode));

        var progress = new ReaderProgress(
            session.CurrentPageIndex,
            session.ScrollOffset,
            session.ZoomMode,
            session.ZoomPercent,
            mode);
        _progressService.ScheduleSave(session.BookId, progress);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        IPdfRenderer? renderer = Interlocked.Exchange(ref _currentRenderer, null);
        if (renderer is IPdfRendererHealth health)
        {
            health.Recovered -= OnRendererRecovered;
        }

        _events.Dispose();
        renderer?.Dispose();
    }

    private void TriggerPrefetch(ReaderSession session)
    {
        IEnumerable<int> pages = GetPrefetchRange(session);

        // Must match the reader surface's render width so prefetched neighbours are
        // cache hits on the next/previous page turn (NFR-OGMA-005).
        var request = new RenderRequest(RenderWidthPx);
        _renderCache.Prefetch(session.BookId, pages, request);

        // Warm neighbour geometry too: it is scheduled ahead of renders and cached by
        // the renderer, so the next page turn resolves rotation without waiting for an
        // in-flight render (K32).
        if (_currentRenderer is { PageCount: > 0 } renderer)
        {
            foreach (int page in GetPrefetchRange(session).Where(page => page != session.CurrentPageIndex))
            {
                _ = WarmGeometryAsync(renderer, page);
            }
        }
    }

    private static async Task WarmGeometryAsync(IPdfRenderer renderer, int pageIndex) =>
        await GetRotationAsync(renderer, pageIndex, fallback: 0, CancellationToken.None).ConfigureAwait(false);

    private void Publish(ReaderEvent readerEvent)
    {
        // Renderer recovery is reported from worker threads; keep the Rx contract of
        // serialised OnNext calls.
        lock (_eventsSync)
        {
            _events.OnNext(readerEvent);
        }
    }

    private void OnRendererRecovered(object? sender, PdfRendererRecoveredEventArgs e)
    {
        if (_currentSession is { } session && ReferenceEquals(sender, _currentRenderer))
        {
            Publish(new ReaderEvent.EngineRecovered(session.BookId, e.Reason, e.RespawnCount, e.Elapsed));
        }
    }

    private static async Task<int> GetRotationAsync(
        IPdfRenderer? renderer,
        int pageIndex,
        int fallback,
        CancellationToken ct)
    {
        if (renderer is null || renderer.PageCount == 0)
        {
            return fallback;
        }

        try
        {
            PdfPageGeometry geometry = await renderer.GetPageGeometryAsync(pageIndex, ct).ConfigureAwait(false);
            return ((geometry.RotationDegrees % 360) + 360) % 360;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException
                                              or ObjectDisposedException or PdfRendererUnavailableException)
        {
            // Rotation is a presentation hint. A lost or stopped worker must not turn a
            // page turn into a crash; the render path reports the failure to the user.
            return fallback;
        }
    }

    private static async Task DisposeRendererAsync(IPdfRenderer renderer) =>
        await Task.Run(renderer.Dispose, CancellationToken.None).ConfigureAwait(false);

    private static IEnumerable<int> GetKeepRange(ReaderSession session)
    {
        int first = Math.Max(0, session.CurrentPageIndex - KeepWindow);
        int last = Math.Min(session.PageCount - 1, session.CurrentPageIndex + KeepWindow);
        for (int page = first; page <= last; page++)
        {
            yield return page;
        }
    }

    private static IEnumerable<int> GetPrefetchRange(ReaderSession session)
    {
        int cur = session.CurrentPageIndex;
        int max = session.PageCount - 1;
        // Queue the visible page first. The isolated reader worker serializes
        // requests, so putting the neighbours first makes opening feel stalled.
        yield return cur;

        if (cur < max)
        {
            yield return cur + 1;
        }

        if (cur > 0)
        {
            yield return cur - 1;
        }
    }
}
