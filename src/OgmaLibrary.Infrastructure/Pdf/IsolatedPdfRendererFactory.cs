using System.Collections.Concurrent;
using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.Infrastructure.Pdf;

/// <summary>Creates reader adapters backed by the isolated production PDF worker.</summary>
public sealed class IsolatedPdfRendererFactory : IPdfRendererFactory
{
    private readonly PdfWorkerClient _client;

    /// <summary>Initializes the factory with the worker client to use.</summary>
    /// <param name="client">The isolated PDF worker client.</param>
    public IsolatedPdfRendererFactory(PdfWorkerClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <inheritdoc />
    public IPdfRenderer Open(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // Sept-23 Phase 06 (T06.10): inside a per-book processing batch, reuse the book's
        // shared worker session instead of copying the file and starting another process.
        if (PdfDocumentBatch.Find(filePath) is { } batch)
        {
            return new BorrowedPdfRenderer(batch.Acquire(_client));
        }

        return new IsolatedPdfRenderer(_client, filePath, password: null);
    }

    /// <inheritdoc />
    public IPdfRenderer Open(string filePath, char[] password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(password);
        return new IsolatedPdfRenderer(_client, filePath, password);
    }
}

/// <summary>
/// Reader adapter over a supervised persistent worker session. All worker IPC runs on
/// the thread pool and is awaited; page geometry (and therefore rotation) is cached per
/// document because it never changes while the document is open.
/// </summary>
internal sealed class IsolatedPdfRenderer : IPdfRenderer, IPdfRendererHealth
{
    private readonly ReaderSessionSupervisor _supervisor;
    private readonly ConcurrentDictionary<int, PdfPageGeometry> _geometry = new();
    private int _disposed;

    public IsolatedPdfRenderer(PdfWorkerClient client, string filePath, char[]? password)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _supervisor = new ReaderSessionSupervisor(
            sessionPassword => client.OpenSession(filePath, sessionPassword),
            password,
            client.SessionLimits);
        _supervisor.Recovered += OnSupervisorRecovered;
        PageCount = _supervisor.PageCount;
    }

    public event EventHandler<PdfRendererRecoveredEventArgs>? Recovered;

    public int PageCount { get; }

    /// <summary>Gets the supervisor, for diagnostics and tests.</summary>
    internal ReaderSessionSupervisor Supervisor => _supervisor;

    public Task<RenderResult> RenderPageAsync(int pageIndex, RenderRequest request, CancellationToken ct)
    {
        ThrowIfInvalidPage(pageIndex);
        ArgumentNullException.ThrowIfNull(request);
        return RunAsync((session, token) => session.RenderPageAsync(pageIndex, request, token), ct);
    }

    public async Task<int> GetPageRotationDegreesAsync(int pageIndex, CancellationToken ct) =>
        (await GetPageGeometryAsync(pageIndex, ct).ConfigureAwait(false)).RotationDegrees;

    public async Task<PdfPageGeometry> GetPageGeometryAsync(int pageIndex, CancellationToken ct)
    {
        ThrowIfInvalidPage(pageIndex);
        if (_geometry.TryGetValue(pageIndex, out PdfPageGeometry? cached))
        {
            return cached;
        }

        PdfPageGeometry geometry = await RunAsync(
                (session, token) => session.GetPageGeometryAsync(pageIndex, token),
                ct)
            .ConfigureAwait(false);
        return _geometry.GetOrAdd(pageIndex, geometry);
    }

    public Task<PdfDocumentMetadata> ReadDocumentMetadataAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return RunAsync((session, token) => session.ReadDocumentMetadataAsync(token), ct);
    }

    public Task<IReadOnlyList<PdfOutlineEntry>> ReadOutlineAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return RunAsync((session, token) => session.ReadOutlineAsync(token), ct);
    }

    public Task<TextLayer> ExtractTextLayerAsync(int pageIndex, CancellationToken ct)
    {
        ThrowIfInvalidPage(pageIndex);
        return RunAsync((session, token) => session.ExtractTextLayerAsync(pageIndex, token), ct);
    }

    // Synchronous members remain for background ingestion callers (metadata, ISBN,
    // search extraction, OCR). They block the calling thread, so reader and UI code must
    // use the asynchronous members; an architecture test enforces that in Reader and App.
    public int GetPageRotationDegrees(int pageIndex) =>
        GetPageRotationDegreesAsync(pageIndex, CancellationToken.None).GetAwaiter().GetResult();

    public PdfPageGeometry GetPageGeometry(int pageIndex) =>
        GetPageGeometryAsync(pageIndex, CancellationToken.None).GetAwaiter().GetResult();

    public PdfDocumentMetadata ReadDocumentMetadata() =>
        ReadDocumentMetadataAsync(CancellationToken.None).GetAwaiter().GetResult();

    public IReadOnlyList<PdfOutlineEntry> ReadOutline() =>
        ReadOutlineAsync(CancellationToken.None).GetAwaiter().GetResult();

    public TextLayer ExtractTextLayer(int pageIndex) =>
        ExtractTextLayerAsync(pageIndex, CancellationToken.None).GetAwaiter().GetResult();

    public PdfRendererHealthSnapshot GetHealthSnapshot() => _supervisor.GetHealthSnapshot();

    /// <summary>Generates a cover or spine in the supervised session (Sept-23 Phase 06, T06.10).</summary>
    /// <param name="command">The asset command.</param>
    /// <param name="widthPx">The asset width.</param>
    /// <param name="heightPx">The asset height.</param>
    /// <param name="ct">A token to cancel the request while it is queued.</param>
    /// <returns>The verified JPEG bytes.</returns>
    internal Task<byte[]> GenerateAssetAsync(string command, int widthPx, int heightPx, CancellationToken ct) =>
        RunAsync((session, token) => session.GenerateAssetAsync(command, widthPx, heightPx, token), ct);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _supervisor.Recovered -= OnSupervisorRecovered;
        _supervisor.Dispose();
    }

    private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    private Task<T> RunAsync<T>(
        Func<PdfWorkerClient.PdfWorkerSession, CancellationToken, Task<T>> operation,
        CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        // Hop to the thread pool before touching the pipe or the supervisor so a caller
        // on the UI thread never performs worker IPC, respawn or disposal synchronously.
        return Task.Run(() => _supervisor.ExecuteAsync(operation, ct), ct);
    }

    private void ThrowIfInvalidPage(int pageIndex)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);
    }

    private void OnSupervisorRecovered(object? sender, PdfRendererRecoveredEventArgs e) =>
        Recovered?.Invoke(this, e);
}
