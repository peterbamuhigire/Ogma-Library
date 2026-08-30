using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.AI.Advisor;

namespace OgmaLibrary.Tests.Ai;

/// <summary>V2 answer-mode grounding tests.</summary>
public sealed class LocalEvidenceAnswerPipelineTests
{
    [Fact]
    public async Task Answer_UsesOnlyLocalSnippetsAndCitesEveryPassage()
    {
        var pipeline = new LocalEvidenceAnswerPipeline(new FakeSemanticSearchService(
        [
            new SemanticSearchResult("book-1", "Local book", 42, SearchChunkSource.Page, "A local passage.", 0.91f, false, 0.88, PageIndex: 41),
            new SemanticSearchResult("book-2", "Another book", 43, SearchChunkSource.Page, "Another local passage.", 0.71f, false, 0.72, PageIndex: 8),
        ]));

        AnswerResponse response = await pipeline.GetAnswerAsync(new AnswerRequest("What is the idea?", 2), CancellationToken.None);

        Assert.True(response.IsV2);
        Assert.Equal(2, response.Citations.Count);
        Assert.Contains("A local passage.", response.Answer, StringComparison.Ordinal);
        Assert.Equal(42, response.Citations[0].PageNumber);
        Assert.Equal("43", response.Citations[1].ChunkId);
    }

    [Fact]
    public async Task Answer_WithNoEvidence_ReturnsSafeNoEvidenceResponse()
    {
        var pipeline = new LocalEvidenceAnswerPipeline(new FakeSemanticSearchService([]));

        AnswerResponse response = await pipeline.GetAnswerAsync(new AnswerRequest("Unknown"), CancellationToken.None);

        Assert.Empty(response.Citations);
        Assert.Contains("No matching local evidence", response.Answer, StringComparison.Ordinal);
    }

    private sealed class FakeSemanticSearchService(IReadOnlyList<SemanticSearchResult> results) : ISemanticSearchService
    {
        public Task<SemanticSearchResponse> SearchAsync(string queryText, int maxResults, CancellationToken cancellationToken) =>
            Task.FromResult(new SemanticSearchResponse(false, false, results.Take(maxResults).ToArray()));
    }
}
