using OgmaLibrary.Application.Reader;
using AppTextLayer = OgmaLibrary.Application.Reader.TextLayer;

namespace OgmaLibrary.Reader.TextLayer;

/// <summary>
/// Provides in-document text search using cached text layers (FR-READ-006).
/// Scanned pages (ExtractionQuality.Scanned) are suppressed with a notice.
/// </summary>
public sealed class InDocumentSearchService : IInDocumentSearchService
{
    private readonly TextLayerService _textLayerService;
    private readonly IReaderSessionService _session;

    /// <summary>
    /// Initializes a new instance of <see cref="InDocumentSearchService"/>.
    /// </summary>
    /// <param name="textLayerService">The text-layer service.</param>
    /// <param name="session">The reader session service (provides the open renderer).</param>
    public InDocumentSearchService(
        TextLayerService textLayerService,
        IReaderSessionService session)
    {
        ArgumentNullException.ThrowIfNull(textLayerService);
        ArgumentNullException.ThrowIfNull(session);

        _textLayerService = textLayerService;
        _session = session;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchMatch>> SearchAsync(
        string bookId, string query, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        ReaderSession? session = _session.CurrentSession;
        if (session is null || session.BookId != bookId)
        {
            return [];
        }

        int pageCount = session.PageCount;
        string lowerQuery = query.ToLowerInvariant();

        var matches = new List<SearchMatch>();

        for (int page = 0; page < pageCount; page++)
        {
            ct.ThrowIfCancellationRequested();

            AppTextLayer layer = await _textLayerService.ExtractAsync(bookId, page, ct)
                .ConfigureAwait(false);

            if (layer.Quality == ExtractionQuality.Scanned)
            {
                // Add a placeholder match so the UI can show the "no text layer" notice.
                matches.Add(new SearchMatch(
                    page,
                    "No text layer — OCR available in V1",
                    []));
                continue;
            }

            var highlights = new List<HighlightRect>();
            var contextWords = new List<string>();

            foreach (var word in layer.Words)
            {
                if (word.Text.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase))
                {
                    highlights.Add(new HighlightRect(word.Left, word.Top, word.Right, word.Bottom));
                    contextWords.Add(word.Text);
                }
            }

            if (highlights.Count > 0)
            {
                string snippet = string.Join(" ", contextWords.Take(8));
                matches.Add(new SearchMatch(page, snippet, highlights));
            }
        }

        return matches;
    }
}
