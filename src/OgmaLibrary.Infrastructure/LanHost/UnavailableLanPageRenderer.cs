using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Fallback page renderer used when the Reader renderer is not registered.</summary>
internal sealed class UnavailableLanPageRenderer : ILanPageRenderer
{
    /// <inheritdoc />
    public Task<RenderResult?> RenderAsync(
        string bookId,
        int pageNumber,
        RenderRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<RenderResult?>(null);
}
