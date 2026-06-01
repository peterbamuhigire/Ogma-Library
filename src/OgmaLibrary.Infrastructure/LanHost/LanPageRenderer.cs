using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>PDFium-backed LAN page renderer that never exposes raw PDF bytes.</summary>
internal sealed class LanPageRenderer : ILanPageRenderer
{
    private readonly ILanBookFileResolver _fileResolver;
    private readonly IPdfRendererFactory _rendererFactory;

    public LanPageRenderer(ILanBookFileResolver fileResolver, IPdfRendererFactory rendererFactory)
    {
        _fileResolver = fileResolver ?? throw new ArgumentNullException(nameof(fileResolver));
        _rendererFactory = rendererFactory ?? throw new ArgumentNullException(nameof(rendererFactory));
    }

    /// <inheritdoc />
    public async Task<RenderResult?> RenderAsync(
        string bookId,
        int pageNumber,
        RenderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageNumber);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.WidthPx);

        string? path = await _fileResolver.ResolveAsync(bookId, cancellationToken)
            .ConfigureAwait(false);
        if (path is null)
        {
            return null;
        }

        using IPdfRenderer renderer = _rendererFactory.Open(path);
        int pageIndex = pageNumber - 1;
        if (pageIndex >= renderer.PageCount)
        {
            return null;
        }

        return await renderer.RenderPageAsync(pageIndex, request, cancellationToken)
            .ConfigureAwait(false);
    }
}
