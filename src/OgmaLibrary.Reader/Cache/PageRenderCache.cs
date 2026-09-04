using System.Collections.Concurrent;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.Reader.Cache;

/// <summary>
/// Memory-budgeted LRU page-render cache that pre-renders current ± 1 pages and a
/// low-resolution preview so cached page turns complete within the ≤ 100 ms P95
/// budget (NFR-OGMA-005).
/// </summary>
/// <remarks>
/// <para>
/// Cache key: <c>(bookId, pageIndex, widthPx)</c>. Entries are evicted LRU-style
/// once the total bitmap memory exceeds the configured memory budget.
/// </para>
/// <para>
/// All render work is dispatched on the thread pool via <c>Task.Run</c>. Pending
/// renders for pages navigated away from are cancelled via per-slot
/// <see cref="CancellationTokenSource"/> instances.
/// </para>
/// </remarks>
public sealed class PageRenderCache : IPageRenderCache, IDisposable
{
    /// <summary>Default memory budget: 256 MB.</summary>
    public const long DefaultMemoryBudgetBytes = 256L * 1024 * 1024;

    private readonly IPdfRendererFactory _factory;
    private readonly IBenchmarkContext _benchmark;
    private readonly long _memoryBudget;

    // Keyed by (bookId, pageIndex, widthPx)
    private readonly Dictionary<CacheKey, CacheEntry> _entries = new();
    private readonly Dictionary<CacheKey, Task<RenderResult>> _inFlight = new();
    private readonly LinkedList<CacheKey> _lruList = new();
    private readonly Dictionary<CacheKey, LinkedListNode<CacheKey>> _lruNodes = new();
    private readonly ConcurrentDictionary<CacheKey, CancellationTokenSource> _pendingCts = new();

    // Renderer instances are keyed by bookId (one renderer per open document).
    private readonly Dictionary<string, IPdfRenderer> _renderers = new();

    private readonly Lock _syncRoot = new();
    private long _memoryUsageBytes;
    private bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="PageRenderCache"/>.
    /// </summary>
    /// <param name="factory">The renderer factory used to open PDFs.</param>
    /// <param name="benchmark">Performance instrumentation context (NFR-OGMA-005).</param>
    /// <param name="memoryBudgetBytes">
    /// Maximum total bitmap memory. Defaults to <see cref="DefaultMemoryBudgetBytes"/>.
    /// </param>
    public PageRenderCache(
        IPdfRendererFactory factory,
        IBenchmarkContext benchmark,
        long memoryBudgetBytes = DefaultMemoryBudgetBytes)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(benchmark);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(memoryBudgetBytes);

        _factory = factory;
        _benchmark = benchmark;
        _memoryBudget = memoryBudgetBytes;
    }

    /// <inheritdoc />
    public long MemoryUsageBytes => Interlocked.Read(ref _memoryUsageBytes);

    /// <inheritdoc />
    public event EventHandler<RenderCompletedEventArgs>? RenderCompleted;

    /// <inheritdoc />
    public async Task<RenderResult> GetOrRenderAsync(
        string bookId, int pageIndex, RenderRequest request, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);

        var key = new CacheKey(bookId, pageIndex, request.WidthPx);
        using var benchmarkScope = _benchmark.Measure("PageRenderCache.GetOrRender");

        // Check full-res cache.
        lock (_syncRoot)
        {
            if (_entries.TryGetValue(key, out var existing) && existing.IsFull)
            {
                Touch(key);
                return existing.Result!;
            }
        }

        // Check low-res cache while full-res is in flight.
        var lowResKey = new CacheKey(bookId, pageIndex, request.WidthPx / 4);
        lock (_syncRoot)
        {
            if (_entries.TryGetValue(lowResKey, out var preview) && preview.IsFull)
            {
                // Return preview immediately; background render will fire RenderCompleted.
                _ = StartRender(key, bookId, pageIndex, request);
                return preview.Result!;
            }
        }

        // Not cached — join an existing prefetch or start one render. Start the
        // preview and full render together, but return whichever finishes first so
        // a slow scanned page is visible while its crisp version is still warming.
        Task<RenderResult> previewRender = StartRender(
            lowResKey,
            bookId,
            pageIndex,
            request with { IsLowResPreview = true });
        Task<RenderResult> render = StartRender(key, bookId, pageIndex, request);
        Task<RenderResult> first = await Task.WhenAny(previewRender, render)
            .WaitAsync(ct)
            .ConfigureAwait(false);
        return await first.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Prefetch(string bookId, IEnumerable<int> pages, RenderRequest request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(pages);

        foreach (int page in pages)
        {
            var key = new CacheKey(bookId, page, request.WidthPx);
            bool isCached;
            lock (_syncRoot)
            {
                isCached = _entries.TryGetValue(key, out var e) && e.IsFull;
            }

            if (!isCached)
            {
                // First kick off low-res preview.
                var previewRequest = request with { IsLowResPreview = true };
                _ = StartRender(
                    new CacheKey(bookId, page, request.WidthPx / 4),
                    bookId, page, previewRequest);

                // Then queue full-res.
                _ = StartRender(key, bookId, page, request);
            }
        }
    }

    /// <inheritdoc />
    public void CancelOutside(string bookId, IEnumerable<int> keepPages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(keepPages);

        var keepSet = new HashSet<int>(keepPages);

        foreach (var kvp in _pendingCts)
        {
            if (kvp.Key.BookId == bookId && !keepSet.Contains(kvp.Key.PageIndex))
            {
                CancelSafely(kvp.Value);
            }
        }
    }

    /// <inheritdoc />
    public void Invalidate(string bookId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        // Cancel all pending renders for this book.
        foreach (var kvp in _pendingCts)
        {
            if (kvp.Key.BookId == bookId)
            {
                CancelSafely(kvp.Value);
            }
        }

        lock (_syncRoot)
        {
            foreach (CacheKey key in _inFlight.Keys.Where(k => k.BookId == bookId).ToList())
            {
                _inFlight.Remove(key);
            }

            var keysToRemove = _entries.Keys.Where(k => k.BookId == bookId).ToList();
            foreach (var key in keysToRemove)
            {
                RemoveEntry(key);
            }

            if (_renderers.TryGetValue(bookId, out var renderer))
            {
                renderer.Dispose();
                _renderers.Remove(bookId);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var cts in _pendingCts.Values)
        {
            CancelSafely(cts);
            cts.Dispose();
        }

        lock (_syncRoot)
        {
            foreach (var renderer in _renderers.Values)
            {
                renderer.Dispose();
            }

            _renderers.Clear();
            _entries.Clear();
            _inFlight.Clear();
            _lruList.Clear();
            _lruNodes.Clear();
        }

        _pendingCts.Clear();
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private Task<RenderResult> StartRender(
        CacheKey key,
        string bookId,
        int pageIndex,
        RenderRequest request)
    {
        lock (_syncRoot)
        {
            if (_entries.TryGetValue(key, out CacheEntry? existing) && existing.IsFull)
            {
                Touch(key);
                return Task.FromResult(existing.Result!);
            }

            if (_inFlight.TryGetValue(key, out Task<RenderResult>? existingTask))
            {
                return existingTask;
            }

            var completion = new TaskCompletionSource<RenderResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var renderCts = new CancellationTokenSource();
            _inFlight[key] = completion.Task;
            _pendingCts[key] = renderCts;
            _ = RenderAndCompleteAsync(
                key,
                bookId,
                pageIndex,
                request,
                renderCts,
                completion);
            return completion.Task;
        }
    }

    private async Task RenderAndCompleteAsync(
        CacheKey key,
        string bookId,
        int pageIndex,
        RenderRequest request,
        CancellationTokenSource renderCts,
        TaskCompletionSource<RenderResult> completion)
    {
        try
        {
            IPdfRenderer renderer;
            lock (_syncRoot)
            {
                if (!_renderers.TryGetValue(bookId, out renderer!))
                {
                    // Renderer will only be available when the session has set it via SetRenderer.
                    throw new InvalidOperationException(
                        $"No renderer registered for book '{bookId}'. Call SetRenderer before GetOrRenderAsync.");
                }
            }

            RenderResult result = await renderer
                .RenderPageAsync(pageIndex, request, renderCts.Token)
                .ConfigureAwait(false);

            lock (_syncRoot)
            {
                StoreEntry(key, result);
            }

            // Notify on completion (used to update the UI when background renders finish).
            RenderCompleted?.Invoke(this, new RenderCompletedEventArgs(bookId, pageIndex, result));
            completion.TrySetResult(result);
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation — rethrow so caller knows.
            completion.TrySetCanceled();
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
        finally
        {
            if (_pendingCts.TryGetValue(key, out CancellationTokenSource? pending) &&
                ReferenceEquals(pending, renderCts))
            {
                _pendingCts.TryRemove(key, out _);
            }

            lock (_syncRoot)
            {
                if (_inFlight.TryGetValue(key, out Task<RenderResult>? current) &&
                    ReferenceEquals(current, completion.Task))
                {
                    _inFlight.Remove(key);
                }
            }

            renderCts.Dispose();
        }
    }

    /// <summary>
    /// Registers the renderer for a book so the cache can execute renders.
    /// Called by the session service when a document is opened.
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="renderer">The renderer to register.</param>
    public void SetRenderer(string bookId, IPdfRenderer renderer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(renderer);

        lock (_syncRoot)
        {
            if (_renderers.TryGetValue(bookId, out var existing))
            {
                existing.Dispose();
            }

            _renderers[bookId] = renderer;
        }
    }

    private void StoreEntry(CacheKey key, RenderResult result)
    {
        long entryBytes = result.PngBytes.LongLength;

        // Remove existing entry if present (refreshing).
        if (_entries.ContainsKey(key))
        {
            RemoveEntry(key);
        }

        // Evict LRU entries until budget is satisfied.
        while (_lruList.Count > 0 && _memoryUsageBytes + entryBytes > _memoryBudget)
        {
            var oldest = _lruList.First!.Value;
            RemoveEntry(oldest);
        }

        var entry = new CacheEntry(result, entryBytes);
        _entries[key] = entry;
        var node = _lruList.AddLast(key);
        _lruNodes[key] = node;
        Interlocked.Add(ref _memoryUsageBytes, entryBytes);
    }

    private void RemoveEntry(CacheKey key)
    {
        if (!_entries.TryGetValue(key, out var entry))
        {
            return;
        }

        _entries.Remove(key);
        if (_lruNodes.TryGetValue(key, out var node))
        {
            _lruList.Remove(node);
            _lruNodes.Remove(key);
        }

        Interlocked.Add(ref _memoryUsageBytes, -entry.SizeBytes);
    }

    private void Touch(CacheKey key)
    {
        if (_lruNodes.TryGetValue(key, out var node))
        {
            _lruList.Remove(node);
            var newNode = _lruList.AddLast(key);
            _lruNodes[key] = newNode;
        }
    }

    private static void CancelSafely(CancellationTokenSource cts)
    {
        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A render task can dispose its CTS while cancellation is being broadcast.
        }
    }

    // ── Inner types ───────────────────────────────────────────────────────────────

    private readonly record struct CacheKey(string BookId, int PageIndex, int WidthPx);

    private sealed class CacheEntry
    {
        public CacheEntry(RenderResult? result, long sizeBytes)
        {
            Result = result;
            SizeBytes = sizeBytes;
            IsFull = result is not null;
        }

        public RenderResult? Result { get; }
        public long SizeBytes { get; }
        public bool IsFull { get; }
    }
}
