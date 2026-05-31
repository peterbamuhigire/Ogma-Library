using OgmaLibrary.Application.Reader;
using AppTextLayer = OgmaLibrary.Application.Reader.TextLayer;

namespace OgmaLibrary.Reader.TextLayer;

/// <summary>
/// Extracts and caches text layers for PDF pages via <see cref="IPdfRendererFactory"/>
/// (which delegates to PdfPig under the hood). Caches results in memory; persistent
/// sidecar caching is a Phase 10 optimization.
/// </summary>
public sealed class TextLayerService : ITextLayerService
{
    private readonly IPdfRendererFactory _rendererFactory;
    private readonly IReaderSessionService? _session;

    // In-memory cache: bookId → pageIndex → TextLayer
    private readonly Dictionary<string, Dictionary<int, AppTextLayer>> _cache = new();
    private readonly Lock _lock = new();

    /// <summary>
    /// Initializes a new instance of <see cref="TextLayerService"/>.
    /// </summary>
    /// <param name="rendererFactory">The renderer factory for opening PDFs.</param>
    /// <param name="session">Optional active reader session for direct extraction from the open renderer.</param>
    public TextLayerService(IPdfRendererFactory rendererFactory, IReaderSessionService? session = null)
    {
        ArgumentNullException.ThrowIfNull(rendererFactory);
        _rendererFactory = rendererFactory;
        _session = session;
    }

    /// <inheritdoc />
    public async Task<AppTextLayer> ExtractAsync(string bookId, int pageIndex, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);

        // Check in-memory cache.
        lock (_lock)
        {
            if (_cache.TryGetValue(bookId, out var pageCache) &&
                pageCache.TryGetValue(pageIndex, out var cached))
            {
                return cached;
            }
        }

        ct.ThrowIfCancellationRequested();

        // Extraction is CPU-bound; run off-thread.
        AppTextLayer layer = await Task.Run(() =>
        {
            // Re-check cache inside the task (avoid duplicate extraction on concurrent calls).
            lock (_lock)
            {
                if (_cache.TryGetValue(bookId, out var pc) &&
                    pc.TryGetValue(pageIndex, out var c))
                {
                    return c;
                }
            }

            if (_session?.CurrentSession?.BookId == bookId &&
                _session.CurrentRenderer is { } renderer)
            {
                return renderer.ExtractTextLayer(pageIndex);
            }

            return new AppTextLayer(pageIndex, [], ExtractionQuality.Empty);
        }, ct).ConfigureAwait(false);

        lock (_lock)
        {
            if (!_cache.TryGetValue(bookId, out var pageCache))
            {
                pageCache = new Dictionary<int, AppTextLayer>();
                _cache[bookId] = pageCache;
            }

            pageCache[pageIndex] = layer;
        }

        return layer;
    }

    /// <inheritdoc />
    public async Task<ExtractionQuality> GetQualityAsync(
        string bookId, int pageIndex, CancellationToken ct)
    {
        AppTextLayer layer = await ExtractAsync(bookId, pageIndex, ct).ConfigureAwait(false);
        return layer.Quality;
    }

    /// <summary>
    /// Extracts the text layer directly from an open <see cref="IPdfRenderer"/>, bypassing
    /// the factory. Called by <see cref="InDocumentSearchService"/> which already has the
    /// open renderer from the active session.
    /// </summary>
    /// <param name="bookId">The stable book identity (for cache keying).</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="renderer">The open renderer for the document.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The text layer.</returns>
    public async Task<AppTextLayer> ExtractWithRendererAsync(
        string bookId,
        int pageIndex,
        IPdfRenderer renderer,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentNullException.ThrowIfNull(renderer);

        // Check cache first.
        lock (_lock)
        {
            if (_cache.TryGetValue(bookId, out var pc) &&
                pc.TryGetValue(pageIndex, out var c))
            {
                return c;
            }
        }

        ct.ThrowIfCancellationRequested();

        AppTextLayer layer = await Task.Run(() => renderer.ExtractTextLayer(pageIndex), ct)
            .ConfigureAwait(false);

        lock (_lock)
        {
            if (!_cache.TryGetValue(bookId, out var pageCache))
            {
                pageCache = new Dictionary<int, AppTextLayer>();
                _cache[bookId] = pageCache;
            }

            pageCache[pageIndex] = layer;
        }

        return layer;
    }

    /// <summary>Evicts the cached text layers for a book (called on document close).</summary>
    /// <param name="bookId">The stable book identity.</param>
    public void Invalidate(string bookId)
    {
        lock (_lock)
        {
            _cache.Remove(bookId);
        }
    }
}
