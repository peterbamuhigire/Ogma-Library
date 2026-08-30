using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Domain;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Infrastructure.AI.Advisor;

/// <summary>
/// V2 extractive answer pipeline. It never invents a response: the displayed
/// answer is assembled from locally indexed passages and every passage is cited.
/// </summary>
public sealed class LocalEvidenceAnswerPipeline : IAnswerPipeline
{
    private const string EvidenceVersion = "advisor-evidence-v1";
    private const int MaximumExcerptLength = 512;

    private readonly ISemanticSearchService _search;

    /// <summary>Initializes the local-evidence pipeline.</summary>
    public LocalEvidenceAnswerPipeline(ISemanticSearchService search)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
    }

    /// <inheritdoc />
    public async Task<AnswerResponse> GetAnswerAsync(
        AnswerRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        SemanticSearchResponse searchResponse = await _search
            .SearchAsync(request.Question, request.MaxCitations, cancellationToken)
            .ConfigureAwait(false);

        HashSet<string> seen = new(StringComparer.Ordinal);
        List<AnswerCitation> citations = [];
        IEnumerable<SemanticSearchResult> eligibleResults = searchResponse.Results
            .Where(result => request.AllowContentAwareTier || result.Source is null or SearchChunkSource.Tag or SearchChunkSource.Description);
        foreach (SemanticSearchResult result in eligibleResults)
        {
            if (string.IsNullOrWhiteSpace(result.BookId) || string.IsNullOrWhiteSpace(result.Snippet))
            {
                continue;
            }

            AnswerCitation citation = ToCitation(result);
            string key = $"{citation.BookId.Value}:{citation.ChunkId ?? citation.PageNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? citation.SourceLabel}";
            if (seen.Add(key))
            {
                citations.Add(citation);
            }

            if (citations.Count >= request.MaxCitations)
            {
                break;
            }
        }

        string answer = citations.Count == 0
            ? "No matching local evidence was found in the library."
            : "Local evidence excerpts (source text; review the citations before treating them as a complete answer): " +
              string.Join(" ", citations.Select((citation, index) =>
                  $"[{index + 1}: {citation.SourceLabel}] {citation.RelevantText}"));

        return new AnswerResponse(answer, citations, IsV2: true);
    }

    private static AnswerCitation ToCitation(SemanticSearchResult result)
    {
        double score = result.HybridScore ?? result.SemanticScore ?? (result.ExactFallback ? 0.65 : 0.5);
        score = Math.Clamp(score, 0.0, 1.0);
        string sourceLabel = result.Source switch
        {
            SearchChunkSource.Page => "page",
            SearchChunkSource.Note => "reader-note",
            SearchChunkSource.Tag => "tag",
            SearchChunkSource.Description => "description",
            SearchChunkSource.Toc => "table-of-contents",
            _ => "search-result",
        };
        string? uncertainty = result.ExactFallback
            ? "Exact-text fallback; semantic relevance was unavailable."
            : result.Source is null
                ? "Source category was not supplied by the search result."
                : null;

        int? pageNumber = result.PageIndex is >= 0 ? result.PageIndex.Value + 1 : null;
        return new AnswerCitation(
            new BookId(result.BookId),
            pageNumber,
            result.ChunkId is > 0 ? result.ChunkId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
            SanitizeExcerpt(result.Snippet!),
            new ConfidenceScore(score),
            sourceLabel,
            EvidenceVersion,
            uncertainty);
    }

    private static string SanitizeExcerpt(string text)
    {
        string cleaned = new(text
            .Where(character => !char.IsControl(character) || character is '\r' or '\n' or '\t')
            .ToArray());
        string normalized = string.Join(' ', cleaned.Split(
            [' ', '\r', '\n', '\t'],
            StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= MaximumExcerptLength
            ? normalized
            : normalized[..(MaximumExcerptLength - 1)].TrimEnd() + "…";
    }
}
