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
    private readonly PdfWorkerClient _client;
    private readonly string _filePath;
    private readonly char[]? _password;
    private bool _disposed;

    public IsolatedPdfRenderer(PdfWorkerClient client, string filePath, char[]? password)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _client = client;
        _filePath = Path.GetFullPath(filePath);
        _password = password?.ToArray();
        PageCount = _client.GetPageCount(_filePath, _password);
    }

    public int PageCount { get; }

    public Task<RenderResult> RenderPageAsync(int pageIndex, RenderRequest request, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);
        return _client.RenderPageAsync(_filePath, pageIndex, request, _password, ct);
    }

    public int GetPageRotationDegrees(int pageIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);
        return _client.GetPageRotationDegrees(_filePath, pageIndex, _password);
    }

    public TextLayer ExtractTextLayer(int pageIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);
        return _client.ExtractTextLayer(_filePath, pageIndex, _password);
    }

    public void Dispose()
    {
        if (_password is not null)
        {
            Array.Clear(_password);
        }

        _disposed = true;
    }
}
