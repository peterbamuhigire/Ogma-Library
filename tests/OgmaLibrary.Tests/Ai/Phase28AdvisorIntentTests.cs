using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Domain;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.AI.Advisor;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 28 intent, retrieval, constraint and provider-off proofs.</summary>
public sealed class Phase28AdvisorIntentTests
{
    [Theory]
    [InlineData("Something explaining the fall of empires.", "fall", "")]
    [InlineData("Something thoughtful but not depressing.", "thoughtful", "depressing")]
    [InlineData("Teach me economics without assuming I studied economics.", "economics", "")]
    [InlineData("Something short I can finish this weekend.", "", "")]
    [InlineData("Something like Guns, Germs and Steel but less deterministic.", "Guns, Germs and Steel", "deterministic")]
    [InlineData("African political history after independence, focused on institutions rather than biographies.", "institutions", "biographies")]
    [InlineData("Something on AI, but not a programming textbook.", "AI", "programming")]
    [InlineData("Surprise me with something I probably would not normally choose.", "surprise", "")]
    public void Parser_ProducesStructuredIntentForBenchmarkCategories(
        string query,
        string expectedPositiveOrReference,
        string expectedNegative)
    {
        AdvisorIntent intent = AdvisorIntentParser.Parse(query);

        Assert.Equal(AdvisorIntent.SchemaVersion, intent.Version);
        if (query.Contains("like ", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Contains(expectedPositiveOrReference, intent.ComparisonReference, StringComparison.OrdinalIgnoreCase);
        }
        else if (query.Contains("surprise", StringComparison.OrdinalIgnoreCase))
        {
            Assert.True(intent.IsBroadDiscovery);
        }
        else if (query.Contains("thoughtful", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Contains(expectedPositiveOrReference, intent.MoodTerms);
        }
        else if (query.Contains("short", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Equal(AdvisorLengthPreference.ShortBook, intent.Length);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(expectedPositiveOrReference))
            {
                Assert.Contains(expectedPositiveOrReference.ToLowerInvariant(), intent.PositiveTerms);
            }
        }

        if (!string.IsNullOrWhiteSpace(expectedNegative))
        {
            Assert.Contains(expectedNegative.ToLowerInvariant(), intent.NegativeTerms);
        }
    }

    [Fact]
    public void Parser_RecognizesDifficultyAndLengthConstraints()
    {
        AdvisorIntent introductory = AdvisorIntentParser.Parse("Teach me economics without assuming I studied economics.");
        AdvisorIntent shortBook = AdvisorIntentParser.Parse("Something short I can finish this weekend.");

        Assert.Equal(DifficultyLabel.Introductory, introductory.Difficulty);
        Assert.DoesNotContain("economics", introductory.NegativeTerms);
        Assert.Equal(AdvisorLengthPreference.ShortBook, shortBook.Length);
    }

    [Fact]
    public void CandidateRanker_EnforcesNegativeAndKnownLengthConstraints()
    {
        BookMetadataDto[] candidates =
        [
            Candidate("BOOK-P28-LONG", "AI Programming Textbook", ["AI", "programming", "textbook"], 700),
            Candidate("BOOK-P28-SHORT", "AI for Society", ["AI", "social science"], 180),
        ];

        AdvisorIntent intent = AdvisorIntentParser.Parse("Something short on AI, but not a programming textbook.");

        IReadOnlyList<BookMetadataDto> ranked = AdvisorCandidateRanker.Rank(candidates, intent, 10);

        Assert.Equal(["BOOK-P28-SHORT"], ranked.Select(candidate => candidate.BookId));
    }

    [Fact]
    public void CandidateRanker_DiversifiesBroadDiscoveryByAuthor()
    {
        BookMetadataDto[] candidates =
        [
            Candidate("BOOK-P28-A", "A First Book", ["history"], 200) with { Authors = ["Same Author"] },
            Candidate("BOOK-P28-B", "B Same Author", ["history"], 200) with { Authors = ["Same Author"] },
            Candidate("BOOK-P28-C", "C Different Author", ["history"], 200) with { Authors = ["Different Author"] },
        ];

        IReadOnlyList<BookMetadataDto> ranked = AdvisorCandidateRanker.Rank(
            candidates,
            AdvisorIntentParser.Parse("Surprise me with something unusual."),
            3);

        Assert.NotEqual(ranked[0].Authors[0], ranked[1].Authors[0]);
        Assert.Contains(ranked.Take(2), candidate => candidate.BookId == "BOOK-P28-C");
    }

    [Fact]
    public void CandidateRanker_UsesResolvedReferenceSignalsDeterministically()
    {
        BookMetadataDto reference = Candidate("BOOK-P28-REFERENCE", "Reference Work", ["systems"], 300)
            with { Authors = ["Reference Author"], Categories = ["History"], Tags = ["institutions"] };
        BookMetadataDto[] candidates =
        [
            Candidate("BOOK-P28-RELATED", "Related Work", ["institutions"], 300)
                with { Authors = ["Reference Author"], Categories = ["History"], Tags = ["politics"] },
            Candidate("BOOK-P28-OTHER", "Other Work", ["systems"], 300)
                with { Authors = ["Other Author"], Categories = ["Science"], Tags = ["technology"] },
        ];

        IReadOnlyList<BookMetadataDto> ranked = AdvisorCandidateRanker.Rank(
            candidates,
            AdvisorIntentParser.Parse("Something like Reference Work"),
            2,
            reference);

        Assert.Equal("BOOK-P28-RELATED", ranked[0].BookId);
        Assert.Equal("BOOK-P28-OTHER", ranked[1].BookId);
    }

    [Fact]
    public async Task Reader_UnionsSemanticCandidates_AndRejectsUnavailableBooks()
    {
        AdvisorCatalogueReader reader = new(
            new FakeCatalogue(
                new Dictionary<string, BookDetailProjection>(StringComparer.Ordinal)
                {
                    ["BOOK-P28-METADATA"] = Detail("BOOK-P28-METADATA", "Literal Match", status: 0),
                    ["BOOK-P28-CONCEPT"] = Detail("BOOK-P28-CONCEPT", "Concept Match", status: 0),
                    ["BOOK-P28-UNAVAILABLE"] = Detail("BOOK-P28-UNAVAILABLE", "Unavailable", status: 1),
                }),
            new FakeMetadataSearch([new MetadataSearchResult("BOOK-P28-METADATA", "Literal Match", null, 1, ["Title"])]),
            new FakeSemanticSearch(new SemanticSearchResponse(
                false,
                false,
                new SemanticSearchResult[]
                {
                    new("BOOK-P28-CONCEPT", "Concept Match", null, null, "concept", 0.9f, false),
                    new("BOOK-P28-UNAVAILABLE", "Unavailable", null, null, "concept", 0.8f, false),
                })));

        IReadOnlyList<BookMetadataDto> candidates = await reader.GetCandidatesAsync(
            new RecommendationQuery("conceptual systems"),
            CancellationToken.None);

        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, candidate => candidate.BookId == "BOOK-P28-CONCEPT");
        Assert.DoesNotContain(candidates, candidate => candidate.BookId == "BOOK-P28-UNAVAILABLE");
    }

    [Fact]
    public async Task Fallback_ReturnsGroundedCards_WhenProviderIsOff()
    {
        BookMetadataDto[] candidates =
        [
            Candidate("BOOK-P28-AI", "AI for Society", ["AI", "society"], 180),
            Candidate("BOOK-P28-PROGRAMMING", "AI Programming Textbook", ["AI", "programming", "textbook"], 700),
        ];
        RecommendationPipeline pipeline = new(
            new FakeCatalogueReader(candidates),
            new MetadataPayloadEnricher(),
            new DisabledGateway(),
            new RecommendationResponseParser(),
            new RecommendationProvenanceValidator(),
            new RecommendationStructuralValidator(),
            new EmptyHybridRanker(),
            new HybridRecommendationMerger());

        IReadOnlyList<RecommendationCard> cards = await pipeline.GetRecommendationsAsync(
            new RecommendationQuery("Something on AI, but not a programming textbook.", maxResults: 2),
            new RecommendationGenerationOptions(AiPrivacyTier.MetadataOnly, "disabled", "off"),
            CancellationToken.None);

        RecommendationCard card = Assert.Single(cards);
        Assert.Equal("BOOK-P28-AI", card.BookId.Value);
        Assert.Equal("local-advisor-v1", card.Explanation.ModelUsed);
        Assert.True(new RecommendationStructuralValidator().Validate(cards).IsValid);
        Assert.All(card.Explanation.ProvenanceItems, item => Assert.Equal(card.BookId, item.BookId));
    }

    private static BookMetadataDto Candidate(string id, string title, IReadOnlyList<string> tags, int pages) =>
        new(id, title, ["Ogma Test"], tags, ["Education"], $"Description for {title}.", null, 2026, [], null, pages);

    private static BookDetailProjection Detail(string id, string title, int status) =>
        new(id, title, ["Ogma Test"], 2026, null, null, null, status, null, $"books/{id}.pdf", null, null, null, 0,
            [new MetadataFieldProjection("Tags", "concepts", "Test", 1.0, false), new MetadataFieldProjection("Pages", "240", "Test", 1.0, false)],
            IsAvailable: status == 0);

    private sealed class FakeMetadataSearch(IReadOnlyList<MetadataSearchResult> results) : IMetadataSearchService
    {
        public Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(string? query, CancellationToken cancellationToken) =>
            Task.FromResult(results);
    }

    private sealed class FakeSemanticSearch(SemanticSearchResponse response) : ISemanticSearchService
    {
        public Task<SemanticSearchResponse> SearchAsync(string queryText, int maxResults, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }

    private sealed class FakeCatalogue(IReadOnlyDictionary<string, BookDetailProjection> details) : ICatalogueReadModel
    {
        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            foreach (BookDetailProjection detail in details.Values.Where(detail => detail.Status == 0))
            {
                yield return new BookSummaryProjection(detail.BookId, detail.Title, detail.Authors, null, detail.Status, null, [], null, true, detail.Year);
            }
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult(details.GetValueOrDefault(bookId));

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);
    }

    private sealed class FakeCatalogueReader(IReadOnlyList<BookMetadataDto> candidates) : IAdvisorCatalogueReader
    {
        public Task<IReadOnlyList<BookMetadataDto>> GetCandidatesAsync(RecommendationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(candidates);
    }

    private sealed class DisabledGateway : IAiGateway
    {
        public Task<AiCompletion> SendAsync(AiRequest request, CancellationToken cancellationToken) =>
            throw new AiDisabledException();
    }

    private sealed class EmptyHybridRanker : IHybridRankerConsumer
    {
        public Task<IReadOnlyList<RankedCandidate>> RankAsync(RecommendationQuery query, IReadOnlyList<BookMetadataDto> candidates, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RankedCandidate>>([]);
    }
}
