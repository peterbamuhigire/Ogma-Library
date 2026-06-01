using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.AI.Advisor;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 13 metadata-only recommendation pipeline tests.</summary>
public sealed class RecommendationPipelineTests
{
    [Fact]
    public async Task RecommendationPipeline_MVP_StructuralOracle()
    {
        BookMetadataDto[] candidates =
        [
            Candidate("BOOK-P13-PIPE-001", "Thinking in Systems", ["systems", "learning"]),
            Candidate("BOOK-P13-PIPE-002", "The Pragmatic Programmer", ["craft"]),
        ];
        var gateway = new FakeAiGateway(
            """
            [
              {
                "book_id": "BOOK-P13-PIPE-001",
                "rank": 1,
                "confidence": 0.86,
                "explanation": "Matches the request for systems thinking.",
                "provenance": [
                  { "book_id": "BOOK-P13-PIPE-001", "field": "Tags", "field_value": "systems" }
                ]
              }
            ]
            """);
        RecommendationPipeline pipeline = CreatePipeline(candidates, gateway);

        IReadOnlyList<RecommendationCard> cards = await pipeline.GetRecommendationsAsync(
            new RecommendationQuery("systems thinking"),
            new RecommendationGenerationOptions(AiPrivacyTier.MetadataOnly, "openai", "gpt-test"),
            CancellationToken.None);

        Assert.Single(cards);
        Assert.Equal("BOOK-P13-PIPE-001", cards[0].BookId.Value);
        Assert.Equal(ConfidenceLabel.High, cards[0].Confidence.Label);
        Assert.NotNull(gateway.LastRequest);
        Assert.Equal("recommendation", gateway.LastRequest!.QueryType);
        Assert.Empty(gateway.LastRequest.ContentChunks);
        Assert.Contains("prompt.template", gateway.LastRequest.MetadataFields.Keys);
        Assert.Contains("books.0.title", gateway.LastRequest.MetadataFields.Keys);
    }

    [Fact]
    public async Task HybridPath_MergesSemanticAndAiScores()
    {
        BookMetadataDto[] candidates =
        [
            Candidate("BOOK-P13-HYBRID-001", "AI First", ["general"]),
            Candidate("BOOK-P13-HYBRID-002", "Semantic First", ["systems"]),
        ];
        var gateway = new FakeAiGateway(
            """
            [
              {
                "book_id": "BOOK-P13-HYBRID-001",
                "rank": 1,
                "confidence": 0.90,
                "explanation": "The provider ranked this first.",
                "provenance": [
                  { "book_id": "BOOK-P13-HYBRID-001", "field": "Title", "field_value": "AI First" }
                ]
              },
              {
                "book_id": "BOOK-P13-HYBRID-002",
                "rank": 2,
                "confidence": 0.70,
                "explanation": "The provider ranked this second.",
                "provenance": [
                  { "book_id": "BOOK-P13-HYBRID-002", "field": "Tags", "field_value": "systems" }
                ]
              }
            ]
            """);
        RecommendationPipeline pipeline = CreatePipeline(
            candidates,
            gateway,
            new FakeHybridRanker(
                [
                    new RankedCandidate(candidates[0], 0.05, 0.05),
                    new RankedCandidate(candidates[1], 1.0, 1.0),
                ]));

        IReadOnlyList<RecommendationCard> cards = await pipeline.GetRecommendationsAsync(
            new RecommendationQuery("systems", maxResults: 2),
            new RecommendationGenerationOptions(
                AiPrivacyTier.MetadataOnly,
                "openai",
                "gpt-test",
                new AdvisorOptions(useHybridRanking: true, aiWeight: 0.4, semanticWeight: 0.6)),
            CancellationToken.None);

        Assert.Equal("BOOK-P13-HYBRID-002", cards[0].BookId.Value);
        Assert.Equal(1, cards[0].Rank);
        Assert.Contains(cards[0].Explanation.ProvenanceItems, item => item.MatchField == RecommendationMatchField.SemanticScore);
    }

    [Fact]
    public void ProvenanceValidator_Strips_HallucinatedIds()
    {
        BookMetadataDto[] candidates =
        [
            Candidate("BOOK-P13-LOCAL-001", "Local One", ["local"]),
            Candidate("BOOK-P13-LOCAL-002", "Local Two", ["local"]),
        ];
        RecommendationCard card = new(
            new BookId("BOOK-P13-LOCAL-001"),
            1,
            new ConfidenceScore(0.8),
            new RecommendationExplanation(
                "Local recommendation.",
                [
                    new ProvenanceItem(new BookId("BOOK-P13-LOCAL-001"), RecommendationMatchField.Title, "Local One"),
                    new ProvenanceItem(new BookId("BOOK-P13-HALLUCINATED"), RecommendationMatchField.Title, "Not local"),
                ],
                "gpt-test",
                AiPrivacyTier.MetadataOnly));
        RecommendationProvenanceValidator validator = new();

        IReadOnlyList<RecommendationCard> sanitized = validator.Validate(
            [card],
            candidates,
            5,
            "gpt-test",
            AiPrivacyTier.MetadataOnly);

        RecommendationCard sanitizedCard = Assert.Single(sanitized);
        ProvenanceItem provenance = Assert.Single(sanitizedCard.Explanation.ProvenanceItems);
        Assert.Equal("BOOK-P13-LOCAL-001", provenance.BookId.Value);
    }

    [Fact]
    public void ProvenanceValidator_FallsBack_WhenMostIdsAreHallucinated()
    {
        BookMetadataDto[] candidates =
        [
            Candidate("BOOK-P13-LOCAL-001", "Local One", ["local"]),
            Candidate("BOOK-P13-LOCAL-002", "Local Two", ["local"]),
        ];
        RecommendationCard card = new(
            new BookId("BOOK-P13-HALLUCINATED"),
            1,
            new ConfidenceScore(0.8),
            new RecommendationExplanation(
                "Bad recommendation.",
                [
                    new ProvenanceItem(new BookId("BOOK-P13-HALLUCINATED"), RecommendationMatchField.Title, "Not local"),
                ],
                "gpt-test",
                AiPrivacyTier.MetadataOnly));
        RecommendationProvenanceValidator validator = new();

        IReadOnlyList<RecommendationCard> fallback = validator.Validate(
            [card],
            candidates,
            2,
            "gpt-test",
            AiPrivacyTier.MetadataOnly);

        Assert.Equal(2, fallback.Count);
        Assert.All(fallback, recommendation =>
            Assert.Contains(candidates, candidate => candidate.BookId == recommendation.BookId.Value));
    }

    [Fact]
    public void MetadataPayloadEnricher_BoundsCandidatesAndLoadsPrompt()
    {
        BookMetadataDto[] candidates = Enumerable
            .Range(0, 60)
            .Select(index => Candidate($"BOOK-P13-{index:000}", $"Book {index}", ["tag"]))
            .ToArray();
        MetadataPayloadEnricher enricher = new();

        MetadataPayload payload = enricher.BuildPayload(candidates);
        string prompt = RecommendationPromptTemplate.Load();

        Assert.Equal(50, payload.Candidates.Count);
        Assert.Equal("50", payload.MetadataFields["books.count"]);
        Assert.True(payload.EstimatedCharacters <= 12_500);
        Assert.Contains("Return strict JSON only", prompt, StringComparison.Ordinal);
    }

    private static RecommendationPipeline CreatePipeline(
        IReadOnlyList<BookMetadataDto> candidates,
        IAiGateway gateway,
        IHybridRankerConsumer? hybridRanker = null) =>
        new(
            new FakeCatalogueReader(candidates),
            new MetadataPayloadEnricher(),
            gateway,
            new RecommendationResponseParser(),
            new RecommendationProvenanceValidator(),
            new RecommendationStructuralValidator(),
            hybridRanker ?? new FakeHybridRanker([]),
            new HybridRecommendationMerger());

    private static BookMetadataDto Candidate(string bookId, string title, IReadOnlyList<string> tags) =>
        new(
            bookId,
            title,
            ["Chwezi Core Systems"],
            tags,
            ["Education"],
            $"Description for {title}.",
            null,
            2026,
            [],
            null);

    private sealed class FakeCatalogueReader(IReadOnlyList<BookMetadataDto> candidates) : IAdvisorCatalogueReader
    {
        public Task<IReadOnlyList<BookMetadataDto>> GetCandidatesAsync(
            RecommendationQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult(candidates);
    }

    private sealed class FakeAiGateway(string responseText) : IAiGateway
    {
        public AiRequest? LastRequest { get; private set; }

        public Task<AiCompletion> SendAsync(AiRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new AiCompletion(responseText, 100, 50));
        }
    }

    private sealed class FakeHybridRanker(IReadOnlyList<RankedCandidate> rankedCandidates) : IHybridRankerConsumer
    {
        public Task<IReadOnlyList<RankedCandidate>> RankAsync(
            RecommendationQuery query,
            IReadOnlyList<BookMetadataDto> candidates,
            CancellationToken cancellationToken) =>
            Task.FromResult(rankedCandidates);
    }
}
