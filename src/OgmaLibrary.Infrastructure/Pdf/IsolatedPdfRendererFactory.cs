using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.Infrastructure.Pdf;

internal sealed class IsolatedPdfRendererFactory : IPdfRendererFactory
{
    private readonly PdfWorkerClient _client;

    public IsolatedPdfRendererFactory(PdfWorkerClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    public IPdfRenderer Open(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return new IsolatedPdfRenderer(_client, filePath, password: null);
    }

    public IPdfRenderer Open(string filePath, char[] password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(password);
        return new IsolatedPdfRenderer(_client, filePath, password);
    }
}

internal sealed class IsolatedPdfRenderer : IPdfRenderer
{
    private readonly PdfWorkerClient.PdfWorkerSession _session;
    private bool _disposed;

    public IsolatedPdfRenderer(PdfWorkerClient client, string filePath, char[]? password)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _session = client.OpenSession(filePath, password);
        PageCount = _session.PageCount;
    }

    public int PageCount { get; }

    public Task<RenderResult> RenderPageAsync(int pageIndex, RenderRequest request, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);
        return _session.RenderPageAsync(pageIndex, request, ct);
    }

    public int GetPageRotationDegrees(int pageIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);
        return _session.GetPageRotationDegrees(pageIndex);
    }

    public TextLayer ExtractTextLayer(int pageIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);
        return _session.ExtractTextLayer(pageIndex);
    }

    public void Dispose()
    {
        _disposed = true;
        _session.Dispose();
    }
}
