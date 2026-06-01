using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Renders catalogue-resolved PDF pages for page-render Host mode.</summary>
internal interface ILanPageRenderer
{
    /// <summary>Renders a 1-based page number for a catalogue book, or null when unavailable.</summary>
    Task<RenderResult?> RenderAsync(
        string bookId,
        int pageNumber,
        RenderRequest request,
        CancellationToken cancellationToken = default);
}
