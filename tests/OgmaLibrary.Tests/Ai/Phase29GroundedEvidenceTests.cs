using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Domain;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.AI.Advisor;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 29 source-labelled evidence and citation validation proofs.</summary>
public sealed class Phase29GroundedEvidenceTests
{
    [Fact]
    public async Task Answer_LabelsSourceVersionAndExactFallbackUncertainty()
    {
        LocalEvidenceAnswerPipeline pipeline = new(new FakeSemanticSearch(
        [
            new SemanticSearchResult("BOOK-P29-001", "Local", 7, SearchChunkSource.Page, "line one\nline two", null, true, PageIndex: 6),
        ]));

        AnswerResponse response = await pipeline.GetAnswerAsync(
            new AnswerRequest("What does the book say?", allowContentAwareTier: true),
            CancellationToken.None);

        AnswerCitation citation = Assert.Single(response.Citations);
        Assert.Equal("page", citation.SourceLabel);
        Assert.Equal("advisor-evidence-v1", citation.EvidenceVersion);
        Assert.Contains("Exact-text fallback", citation.UncertaintyLabel, StringComparison.Ordinal);
        Assert.Contains("[1: page] line one line two", response.Answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Answer_DeduplicatesAndBoundsUntrustedExcerptText()
    {
        string excerpt = new string('x', 600) + "\0";
        LocalEvidenceAnswerPipeline pipeline = new(new FakeSemanticSearch(
        [
            new SemanticSearchResult("BOOK-P29-001", "Local", 7, SearchChunkSource.Description, excerpt, 0.9f, false),
            new SemanticSearchResult("BOOK-P29-001", "Local", 7, SearchChunkSource.Description, excerpt, 0.9f, false),
        ]));

        AnswerResponse response = await pipeline.GetAnswerAsync(
            new AnswerRequest("What does the book say?", 5),
            CancellationToken.None);

        AnswerCitation citation = Assert.Single(response.Citations);
        Assert.Equal(512, citation.RelevantText.Length);
        Assert.DoesNotContain('\0', citation.RelevantText);
    }

    [Fact]
    public async Task Answer_MetadataOnlyMode_ExcludesContentPassages()
    {
        LocalEvidenceAnswerPipeline pipeline = new(new FakeSemanticSearch(
        [
            new SemanticSearchResult("BOOK-P29-PAGE", "Local", 1, SearchChunkSource.Page, "page passage", 0.9f, false),
            new SemanticSearchResult("BOOK-P29-DESC", "Local", 2, SearchChunkSource.Description, "description evidence", 0.8f, false),
        ]));

        AnswerResponse response = await pipeline.GetAnswerAsync(
            new AnswerRequest("What does the metadata say?", 5, allowContentAwareTier: false),
            CancellationToken.None);

        AnswerCitation citation = Assert.Single(response.Citations);
        Assert.Equal("description", citation.SourceLabel);
        Assert.DoesNotContain("page passage", response.Answer, StringComparison.Ordinal);
    }

    [Fact]
    public void RecommendationValidator_RejectsFabricatedFieldClaimsAndMarksFallback()
    {
        const string bookId = "BOOK-P29-001";
        BookMetadataDto candidate = new(
            bookId,
            "Local Title",
            ["Local Author"],
            ["systems"],
            ["Education"],
            "A local description.",
            null,
            2026,
            [],
            null);
        RecommendationCard card = new(
            new BookId(bookId),
            1,
            new ConfidenceScore(0.8),
            new RecommendationExplanation(
                "The provider claims a fabricated title match.",
                [new ProvenanceItem(new BookId(bookId), RecommendationMatchField.Title, "Fabricated Title")],
                "provider-test",
                AiPrivacyTier.MetadataOnly));

        IReadOnlyList<RecommendationCard> result = new RecommendationProvenanceValidator().Validate(
            [card],
            [candidate],
            1,
            "provider-test",
            AiPrivacyTier.MetadataOnly);

        ProvenanceItem provenance = Assert.Single(Assert.Single(result).Explanation.ProvenanceItems);
        Assert.Equal("metadata.title", provenance.SourceLabel);
        Assert.Equal("advisor-evidence-v1", provenance.EvidenceVersion);
        Assert.NotNull(provenance.UncertaintyLabel);
    }

    private sealed class FakeSemanticSearch(IReadOnlyList<SemanticSearchResult> results) : ISemanticSearchService
    {
        public Task<SemanticSearchResponse> SearchAsync(string queryText, int maxResults, CancellationToken cancellationToken) =>
            Task.FromResult(new SemanticSearchResponse(false, false, results.Take(maxResults).ToArray()));
    }
}
