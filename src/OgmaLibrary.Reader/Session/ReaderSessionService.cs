using System.Reactive.Subjects;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Reader.Cache;

namespace OgmaLibrary.Reader.Session;

/// <summary>
/// Manages a single open reader session: opening, resuming, navigating, persisting,
/// and closing a PDF document. Implements FR-READ-001 (open + resume) and feeds the
/// <see cref="IReaderSessionReadModel"/> event stream for LAN-projection readiness.
/// </summary>
public sealed class ReaderSessionService : IReaderSessionService, IReaderSessionReadModel, IDisposable
{
    private readonly IPdfRendererFactory _rendererFactory;
    private readonly IReadingProgressService _progressService;
    private readonly IBookFileLocator _fileLocator;
    private readonly PageRenderCache _renderCache;
    private readonly Subject<ReaderEvent> _events = new();

    private ReaderSession? _currentSession;
    private IPdfRenderer? _currentRenderer;

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
    public ReaderSession? CurrentSession => _currentSession;

    /// <inheritdoc />
    public IPdfRenderer? CurrentRenderer => _currentRenderer;

    /// <inheritdoc />
    public IObservable<ReaderEvent> Events => _events;

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
        // Close any existing session first.
        if (_currentSession is not null)
        {
            await CloseAsync(ct).ConfigureAwait(false);
        }

        // Locate the file.
        string? filePath = await _fileLocator.LocateAsync(bookId, ct).ConfigureAwait(false);
        if (filePath is null)
        {
            throw new FileNotFoundException($"No available PDF file found for book '{bookId}'.");
        }

        // Load saved progress (FR-READ-001 resume).
        ReaderProgress progress = await _progressService.LoadAsync(bookId, ct).ConfigureAwait(false);

        // Open the native renderer.
        IPdfRenderer renderer = password is null
            ? _rendererFactory.Open(filePath)
            : _rendererFactory.Open(filePath, password);
        _currentRenderer = renderer;

        // Register renderer with the cache.
        _renderCache.SetRenderer(bookId, renderer);

        int startPage = pageHint ?? progress.LastPageIndex;
        startPage = Math.Clamp(startPage, 0, Math.Max(0, renderer.PageCount - 1));

        var session = new ReaderSession(
            bookId,
            filePath,
            renderer.PageCount,
            startPage,
            progress.ScrollOffset,
            progress.ZoomMode,
            progress.ZoomPercent,
            progress.DisplayMode,
            GetPageRotationDegrees(renderer, startPage));

        _currentSession = session;

        // Kick off pre-renders for first page and its neighbour.
        TriggerPrefetch(session);

        _events.OnNext(new ReaderEvent.SessionOpened(bookId));
        _events.OnNext(new ReaderEvent.PageChanged(bookId, startPage, renderer.PageCount));

        return session;
    }

    /// <inheritdoc />
    public async Task CloseAsync(CancellationToken ct)
    {
        if (_currentSession is null)
        {
            return;
        }

        string bookId = _currentSession.BookId;

        // Flush progress synchronously for durability (NFR-OGMA-008).
        var progress = new ReaderProgress(
            _currentSession.CurrentPageIndex,
            _currentSession.ScrollOffset,
            _currentSession.ZoomMode,
            _currentSession.ZoomPercent,
            _currentSession.DisplayMode);

        await _progressService.SaveImmediateAsync(bookId, progress, ct).ConfigureAwait(false);

        // Invalidate cache entries for this book.
        _renderCache.Invalidate(bookId);

        _events.OnNext(new ReaderEvent.SessionClosed(bookId));

        _currentRenderer?.Dispose();
        _currentRenderer = null;
        _currentSession = null;
    }

    /// <inheritdoc />
    public async Task NavigateToAsync(int pageIndex, double scrollOffset = 0.0)
    {
        if (_currentSession is null)
        {
            return;
        }

        pageIndex = Math.Clamp(pageIndex, 0, _currentSession.PageCount - 1);

        _currentSession = _currentSession.WithPage(
            pageIndex,
            scrollOffset,
            GetPageRotationDegrees(_currentRenderer, pageIndex));

        // Cancel stale renders for pages outside the ±1 window.
        _renderCache.CancelOutside(
            _currentSession.BookId,
            GetPrefetchRange(_currentSession));

        TriggerPrefetch(_currentSession);

        _events.OnNext(new ReaderEvent.PageChanged(
            _currentSession.BookId, pageIndex, _currentSession.PageCount));

        // Debounce progress write (500 ms).
        var progress = new ReaderProgress(
            pageIndex,
            scrollOffset,
            _currentSession.ZoomMode,
            _currentSession.ZoomPercent,
            _currentSession.DisplayMode);
        _progressService.ScheduleSave(_currentSession.BookId, progress);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void UpdateScrollOffset(double scrollOffset)
    {
        if (_currentSession is null)
        {
            return;
        }

        _currentSession = _currentSession with { ScrollOffset = scrollOffset };

        _events.OnNext(new ReaderEvent.Scrolled(_currentSession.BookId, scrollOffset));

        var progress = new ReaderProgress(
            _currentSession.CurrentPageIndex,
            scrollOffset,
            _currentSession.ZoomMode,
            _currentSession.ZoomPercent,
            _currentSession.DisplayMode);
        _progressService.ScheduleSave(_currentSession.BookId, progress);
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

        _currentSession = _currentSession.WithZoom(mode, percent);

        _events.OnNext(new ReaderEvent.ZoomChanged(_currentSession.BookId, mode, percent));

        var progress = new ReaderProgress(
            _currentSession.CurrentPageIndex,
            _currentSession.ScrollOffset,
            mode,
            percent,
            _currentSession.DisplayMode);
        _progressService.ScheduleSave(_currentSession.BookId, progress);
    }

    /// <summary>Updates display mode and triggers a progress save.</summary>
    /// <param name="mode">The new display mode.</param>
    public void UpdateDisplayMode(DisplayMode mode)
    {
        if (_currentSession is null)
        {
            return;
        }

        _currentSession = _currentSession.WithDisplayMode(mode);

        _events.OnNext(new ReaderEvent.DisplayModeChanged(_currentSession.BookId, mode));

        var progress = new ReaderProgress(
            _currentSession.CurrentPageIndex,
            _currentSession.ScrollOffset,
            _currentSession.ZoomMode,
            _currentSession.ZoomPercent,
            mode);
        _progressService.ScheduleSave(_currentSession.BookId, progress);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _events.Dispose();
        _currentRenderer?.Dispose();
    }

    private void TriggerPrefetch(ReaderSession session)
    {
        var pages = GetPrefetchRange(session);
        var request = new RenderRequest(800);
        _renderCache.Prefetch(session.BookId, pages, request);
    }

    private static int GetPageRotationDegrees(IPdfRenderer? renderer, int pageIndex)
    {
        if (renderer is null)
        {
            return 0;
        }

        int rotation = renderer.GetPageRotationDegrees(pageIndex);
        return ((rotation % 360) + 360) % 360;
    }

    private static IEnumerable<int> GetPrefetchRange(ReaderSession session)
    {
        int cur = session.CurrentPageIndex;
        int max = session.PageCount - 1;
        if (cur > 0)
        {
            yield return cur - 1;
        }

        yield return cur;

        if (cur < max)
        {
            yield return cur + 1;
        }
    }
}
