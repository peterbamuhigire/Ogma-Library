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

        List<AnswerCitation> citations = searchResponse.Results
            .Where(result => !string.IsNullOrWhiteSpace(result.Snippet))
            .Take(request.MaxCitations)
            .Select(ToCitation)
            .ToList();

        string answer = citations.Count == 0
            ? "No matching local evidence was found in the library."
            : string.Join(" ", citations.Select(citation => citation.RelevantText));

        return new AnswerResponse(answer, citations, IsV2: true);
    }

    private static AnswerCitation ToCitation(SemanticSearchResult result)
    {
        double score = result.HybridScore ?? result.SemanticScore ?? (result.ExactFallback ? 0.65 : 0.5);
        score = Math.Clamp(score, 0.0, 1.0);
        return new AnswerCitation(
            new BookId(result.BookId),
            result.PageIndex is null ? null : result.PageIndex.Value + 1,
            result.ChunkId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            result.Snippet!.Trim(),
            new ConfidenceScore(score));
    }
}
