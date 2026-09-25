using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.Infrastructure.Pdf;

/// <summary>
/// An ambient, per-book processing batch (Sept-23 Phase 06, T06.10, K26). While a batch is
/// active for a PDF, every worker-backed operation on that same file — metadata, ISBN and
/// outline reads, page text, cover and spine — runs in <b>one</b> isolated worker session
/// with <b>one</b> sandbox copy and one hash, instead of copying, hashing and launching a
/// process per operation. The isolation boundary is unchanged: one document per worker
/// process, the same sandbox, memory limit and kill-on-close Job Object, and books are never
/// mixed in a worker. The session is closed when the batch ends.
/// </summary>
public sealed class PdfDocumentBatch : IDisposable
{
    private static readonly AsyncLocal<PdfDocumentBatch?> Ambient = new();
    private readonly PdfDocumentBatch? _previous;
    private readonly string? _fullPath;
    private readonly Lock _sync = new();
    private IsolatedPdfRenderer? _renderer;
    private Exception? _openFailure;
    private int _disposed;

    private PdfDocumentBatch(string? fullPath, PdfDocumentBatch? previous)
    {
        _fullPath = fullPath;
        _previous = previous;
    }

    /// <summary>How many operations borrowed the shared session.</summary>
    public int BorrowCount { get; private set; }

    /// <summary>Whether the batch opened its shared worker session.</summary>
    public bool SessionOpened => Volatile.Read(ref _renderer) is not null;

    /// <summary>
    /// Starts a batch for <paramref name="filePath"/> on the current asynchronous flow. A
    /// batch that is already active for the same file is reused (the returned handle is inert).
    /// </summary>
    /// <param name="filePath">The absolute PDF path; an empty or relative path starts an inert batch.</param>
    /// <returns>A handle that ends the batch and closes its session when disposed.</returns>
    public static IDisposable Begin(string? filePath)
    {
        string? fullPath = Normalize(filePath);
        PdfDocumentBatch? current = Ambient.Value;
        if (fullPath is not null && current is not null && current.Matches(fullPath))
        {
            return NoopHandle.Instance;
        }

        var batch = new PdfDocumentBatch(fullPath, current);
        Ambient.Value = batch;
        return batch;
    }

    /// <summary>Returns the active batch for <paramref name="filePath"/>, if any.</summary>
    /// <param name="filePath">The PDF path an operation is about to open.</param>
    /// <returns>The batch, or null.</returns>
    public static PdfDocumentBatch? Find(string? filePath)
    {
        PdfDocumentBatch? current = Ambient.Value;
        if (current is null || Volatile.Read(ref current._disposed) != 0)
        {
            return null;
        }

        string? fullPath = Normalize(filePath);
        return fullPath is not null && current.Matches(fullPath) ? current : null;
    }

    /// <summary>Returns the shared renderer, opening the worker session on first use.</summary>
    /// <param name="client">The worker client that owns the session policy.</param>
    /// <returns>The shared renderer; callers must not dispose it.</returns>
    internal IsolatedPdfRenderer Acquire(PdfWorkerClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            if (_openFailure is not null)
            {
                // A file that cannot be opened (for example a password-protected PDF) is not
                // re-launched for every operation of the batch.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(_openFailure);
            }

            BorrowCount++;
            if (_renderer is not null)
            {
                return _renderer;
            }

            try
            {
                _renderer = new IsolatedPdfRenderer(client, _fullPath!, password: null);
                return _renderer;
            }
            catch (Exception exception) when (exception is PdfPasswordRequiredException or PdfPasswordIncorrectException)
            {
                _openFailure = exception;
                throw;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (ReferenceEquals(Ambient.Value, this))
        {
            Ambient.Value = _previous;
        }

        IsolatedPdfRenderer? renderer;
        lock (_sync)
        {
            renderer = _renderer;
            _renderer = null;
        }

        renderer?.Dispose();
    }

    private bool Matches(string fullPath) =>
        _fullPath is not null &&
        string.Equals(
            _fullPath,
            fullPath,
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static string? Normalize(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !Path.IsPathFullyQualified(filePath))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(filePath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private sealed class NoopHandle : IDisposable
    {
        public static readonly NoopHandle Instance = new();

        public void Dispose()
        {
            // Intentionally empty: the outer batch owns the session.
        }
    }
}

/// <summary>A renderer view over a batch's shared session; disposing it leaves the session open.</summary>
internal sealed class BorrowedPdfRenderer : IPdfRenderer
{
    private readonly IsolatedPdfRenderer _inner;

    public BorrowedPdfRenderer(IsolatedPdfRenderer inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public int PageCount => _inner.PageCount;

    public Task<RenderResult> RenderPageAsync(int pageIndex, RenderRequest request, CancellationToken ct) =>
        _inner.RenderPageAsync(pageIndex, request, ct);

    public int GetPageRotationDegrees(int pageIndex) => _inner.GetPageRotationDegrees(pageIndex);

    public PdfPageGeometry GetPageGeometry(int pageIndex) => _inner.GetPageGeometry(pageIndex);

    public PdfDocumentMetadata ReadDocumentMetadata() => _inner.ReadDocumentMetadata();

    public IReadOnlyList<PdfOutlineEntry> ReadOutline() => _inner.ReadOutline();

    public TextLayer ExtractTextLayer(int pageIndex) => _inner.ExtractTextLayer(pageIndex);

    public Task<int> GetPageRotationDegreesAsync(int pageIndex, CancellationToken ct) =>
        _inner.GetPageRotationDegreesAsync(pageIndex, ct);

    public Task<PdfPageGeometry> GetPageGeometryAsync(int pageIndex, CancellationToken ct) =>
        _inner.GetPageGeometryAsync(pageIndex, ct);

    public Task<PdfDocumentMetadata> ReadDocumentMetadataAsync(CancellationToken ct) =>
        _inner.ReadDocumentMetadataAsync(ct);

    public Task<IReadOnlyList<PdfOutlineEntry>> ReadOutlineAsync(CancellationToken ct) =>
        _inner.ReadOutlineAsync(ct);

    public Task<TextLayer> ExtractTextLayerAsync(int pageIndex, CancellationToken ct) =>
        _inner.ExtractTextLayerAsync(pageIndex, ct);

    public void Dispose()
    {
        // Intentionally empty: the batch closes the shared session when it ends.
    }
}
