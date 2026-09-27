using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Search;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Sept-23 Phase 13 (task 13.1): the audit's query battery (K40) as an automated relevance
/// oracle over the synthetic corpus catalogue, run through the search destination's pipeline.
/// Conceptual (semantic) queries are asserted only when a provider exists (Phase 14).
/// </summary>
public sealed class SearchRelevanceOracleTests : IClassFixture<SearchRelevanceOracleTests.CorpusFixture>
{
    private readonly CorpusFixture _fixture;

    public SearchRelevanceOracleTests(CorpusFixture fixture)
    {
        _fixture = fixture;
    }

    public static TheoryData<string, string> TopOneCases => new()
    {
        { "Lantern", "The Lantern Keeper" },
        { "Namutebi", "Algorithms Explained" },
        { "caravan", "Trade Routes of East Africa" },
        { "dhow Zanzibar", "Trade Routes of East Africa" },
        { "Chwezi dynasty", "A Short History of the Great Lakes Kingdoms" },
        { "algoritms", "Algorithms Explained" },
        { "Wanjirũ", "Hadithi za Jioni: Évening Tales" },
        { "Wanjiru", "Hadithi za Jioni: Évening Tales" },
        { "author:Okello", "The Physics of Everyday Light" },
        // tests/fixtures/corpus/expected.json "searches" (the G4 journey oracle).
        { "Tropical Ecology", "Introduction to Tropical Ecology" },
        { "Grace Namutebi", "Algorithms Explained" },
        { "Zanzibar monsoon", "Trade Routes of East Africa" },
        { "Algoritms Explaned", "Algorithms Explained" },
    };

    [Theory]
    [MemberData(nameof(TopOneCases))]
    public async Task Oracle_ExpectedBookIsTopResult(string query, string expectedTitle)
    {
        IReadOnlyList<string> titles = await _fixture.SearchTitlesAsync(query);

        Assert.True(
            titles.Count > 0 && titles[0] == expectedTitle,
            $"'{query}' → [{string.Join(" | ", titles.Take(3))}], expected top '{expectedTitle}'");
    }

    [Fact]
    public async Task Oracle_BodyWordsFindLanternKeeperInTopTwo()
    {
        IReadOnlyList<string> titles = await _fixture.SearchTitlesAsync("amber library lantern");

        Assert.Contains("The Lantern Keeper", titles.Take(2));
    }

    [Fact]
    public async Task Oracle_DuplicateCopyIsOneResult()
    {
        IReadOnlyList<string> titles = await _fixture.SearchTitlesAsync("Lantern");

        Assert.Single(titles, title => title == "The Lantern Keeper");
    }

    [Fact]
    public async Task Oracle_NonsenseWordReturnsNothing()
    {
        IReadOnlyList<string> titles = await _fixture.SearchTitlesAsync("zzqxnotaword");

        Assert.Empty(titles);
    }

    /// <summary>One migrated corpus catalogue shared by the oracle cases.</summary>
    public sealed class CorpusFixture : IDisposable
    {
        private readonly SyntheticCorpusCatalogue _corpus = SyntheticCorpusCatalogue.Create();

        /// <summary>Runs one query through the search destination's service and returns display titles.</summary>
        public async Task<IReadOnlyList<string>> SearchTitlesAsync(string query)
        {
            UnifiedSearchResponse response = await SearchAsync(query);
            return response.Results.Select(result => result.Title).ToList();
        }

        /// <summary>Runs one query through the unified pipeline with no semantic provider.</summary>
        public Task<UnifiedSearchResponse> SearchAsync(string query) =>
            new UnifiedSearchService(
                    _corpus.Context,
                    new FtsIndexService(_corpus.Context),
                    semantic: null,
                    provider: new AbsentProvider())
                .SearchAsync(query, 30, CancellationToken.None);

        /// <inheritdoc />
        public void Dispose() => _corpus.Dispose();
    }

    private sealed class AbsentProvider : IOllamaEmbeddingProvider
    {
        public string ProviderKey => "ollama";

        public bool IsLocalOnly => true;

        public Task<OllamaEmbeddingResult> EmbedAsync(string text, string modelName, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No provider in the oracle.");

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
