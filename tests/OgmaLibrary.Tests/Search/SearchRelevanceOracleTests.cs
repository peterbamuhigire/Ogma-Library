using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Search;
using OgmaLibrary.Tests.Ai.Onnx;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Sept-23 Phase 13 (task 13.1): the audit's query battery (K40) as an automated relevance
/// oracle over the synthetic corpus catalogue, run through the search destination's pipeline.
/// Conceptual (semantic) queries run through the same pipeline with the built-in ONNX provider
/// (Sept-23 Phase 14, task 14.9) and skip visibly (NOT_ASSESSED) without a configured model.
/// </summary>
public sealed class SearchRelevanceOracleTests :
    IClassFixture<SearchRelevanceOracleTests.CorpusFixture>,
    IClassFixture<SearchRelevanceOracleTests.SemanticFixture>
{
    private readonly CorpusFixture _fixture;
    private readonly SemanticFixture _semantic;

    public SearchRelevanceOracleTests(CorpusFixture fixture, SemanticFixture semantic)
    {
        _fixture = fixture;
        _semantic = semantic;
    }

    /// <summary>
    /// Conceptual queries whose words do not appear in the expected book's title or text:
    /// only similar meaning can place the book in the top three. English and French.
    /// </summary>
    public static TheoryData<string, string> ConceptualTopThreeCases => new()
    {
        { "books about light and colour", "The Physics of Everyday Light" },
        { "rainforest animals", "Introduction to Tropical Ecology" },
        { "precolonial kingdoms", "A Short History of the Great Lakes Kingdoms" },
        { "seafaring merchants and the spice trade", "Trade Routes of East Africa" },
        { "livres sur la lumière et la couleur", "The Physics of Everyday Light" },
        { "animaux de la forêt tropicale", "Introduction to Tropical Ecology" },
        { "royaumes africains précoloniaux", "A Short History of the Great Lakes Kingdoms" },
        { "commerce maritime et marchands de l'océan Indien", "Trade Routes of East Africa" },
    };

    [SkippableTheory]
    [MemberData(nameof(ConceptualTopThreeCases))]
    public async Task Oracle_ConceptualQuery_ExpectedBookInTopThreeBySimilarMeaning(string query, string expectedTitle)
    {
        SemanticCorpusIndex index = await _semantic.GetOrSkipAsync();

        IReadOnlyList<UnifiedSearchResult> results = await index.SearchAsync(query);
        List<UnifiedSearchResult> topThree = results.Take(3).ToList();
        UnifiedSearchResult? hit = topThree.FirstOrDefault(result => result.Title == expectedTitle);

        int rank = results.Select(result => result.Title).ToList().IndexOf(expectedTitle);
        Assert.True(
            hit is not null,
            $"'{query}' → [{string.Join(" | ", topThree.Select(result => result.Title))}], expected '{expectedTitle}' in the top 3 (measured rank {(rank < 0 ? "absent" : (rank + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))})");
        Assert.Contains(hit!.Matches, match => match.Kind == UnifiedMatchKind.Semantic);
    }

    [SkippableTheory]
    [MemberData(nameof(TopOneCases))]
    public async Task Oracle_WithSemanticOn_ExpectedBookIsStillTopResult(string query, string expectedTitle)
    {
        SemanticCorpusIndex index = await _semantic.GetOrSkipAsync();

        List<string> titles = (await index.SearchAsync(query)).Select(result => result.Title).ToList();

        Assert.True(
            titles.Count > 0 && titles[0] == expectedTitle,
            $"semantic on: '{query}' → [{string.Join(" | ", titles.Take(3))}], expected top '{expectedTitle}'");
    }

    [SkippableFact]
    public async Task Oracle_WithSemanticOn_EveryChunkEmbeddedWithoutFailure()
    {
        SemanticCorpusIndex index = await _semantic.GetOrSkipAsync();

        Assert.Equal(0, index.ChunksFailed);
        Assert.True(index.ChunksEmbedded > 0);
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
                    capability: new FakeSemanticCapability(available: false))
                .SearchAsync(query, 30, CancellationToken.None);

        /// <inheritdoc />
        public void Dispose() => _corpus.Dispose();
    }

    /// <summary>
    /// The corpus embedded once with the built-in provider. Built lazily on the first
    /// conceptual case, so keyword-only runs never load the model.
    /// </summary>
    public sealed class SemanticFixture : IDisposable
    {
        private readonly Lazy<Task<SemanticCorpusIndex>> _index = new(SemanticCorpusIndex.CreateOrSkipAsync);

        /// <summary>Returns the embedded corpus, or skips (NOT_ASSESSED) when no model is configured.</summary>
        internal Task<SemanticCorpusIndex> GetOrSkipAsync()
        {
            EmbeddingModelTestFolder.RequireOrSkip();
            return _index.Value;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_index.IsValueCreated && _index.Value.IsCompletedSuccessfully)
            {
                _index.Value.Result.Dispose();
            }
        }
    }
}
