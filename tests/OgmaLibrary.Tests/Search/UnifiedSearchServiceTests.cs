using System.Diagnostics;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Search;

namespace OgmaLibrary.Tests.Search;

/// <summary>Sept-23 Phase 13 (task 13.5): availability model, fusion, filters, OCR labels, coverage.</summary>
public sealed class UnifiedSearchServiceTests : IClassFixture<UnifiedSearchServiceTests.CorpusFixture>
{
    private readonly SyntheticCorpusCatalogue _corpus;

    public UnifiedSearchServiceTests(CorpusFixture fixture)
    {
        _corpus = fixture.Corpus;
    }

    [Fact]
    public async Task ProviderAbsent_ReportsSemanticUnavailableAndStillFindsKeywords()
    {
        UnifiedSearchResponse response = await Service(provider: new Provider(false)).SearchAsync("monsoon", 30, CancellationToken.None);

        Assert.Equal(SemanticSearchState.Unavailable, response.Semantic);
        Assert.Equal("unified-search-v1", response.ContractVersion);
        UnifiedSearchResult first = response.Results[0];
        Assert.Equal("Trade Routes of East Africa", first.Title);
        Assert.Equal("Sarah Achieng", first.Author);
        Assert.Equal(UnifiedMatchKind.Page, first.PrimaryMatch?.Kind);
        Assert.NotNull(first.PageJumpTarget);
        Assert.NotNull(first.Snippet);
        Assert.NotEmpty(first.Snippet!.Spans);
    }

    [Fact]
    public async Task FilterOnlyQuery_IsNotApplicableForSemantic()
    {
        UnifiedSearchResponse response = await Service(provider: new Provider(true)).SearchAsync("author:Okello", 30, CancellationToken.None);

        Assert.Equal(SemanticSearchState.NotApplicable, response.Semantic);
        Assert.Equal("The Physics of Everyday Light", Assert.Single(response.Results).Title);
        Assert.Equal(UnifiedMatchKind.Author, response.Results[0].PrimaryMatch?.Kind);
    }

    [Fact]
    public async Task ProviderAvailable_FusesSemanticResultsWithRrf()
    {
        var semantic = new SemanticStub(
            new SemanticSearchResult("01SYNTHCORPUS0000000000001", null, 1, SearchChunkSource.Page, "light and colour", 0.91f, false, PageIndex: 4));
        UnifiedSearchResponse response = await Service(semantic, new Provider(true))
            .SearchAsync("books about light and colour", 30, CancellationToken.None);

        Assert.Equal(SemanticSearchState.Active, response.Semantic);
        UnifiedSearchResult first = response.Results[0];
        Assert.Equal("The Physics of Everyday Light", first.Title);
        Assert.Contains(first.Matches, match => match.Kind == UnifiedMatchKind.Semantic);
    }

    [Fact]
    public async Task ProviderWithoutEmbeddings_ReportsPreparing()
    {
        var semantic = new SemanticStub([], SemanticSearchAvailability.NoIndex, usedFallback: true);
        UnifiedSearchResponse response = await Service(semantic, new Provider(true)).SearchAsync("caravan", 30, CancellationToken.None);

        Assert.Equal(SemanticSearchState.Preparing, response.Semantic);
        Assert.NotEmpty(response.Results);
    }

    [Fact]
    public async Task SemanticFailure_DoesNotHideKeywordResults()
    {
        UnifiedSearchResponse response = await Service(new ThrowingSemantic(), new Provider(true))
            .SearchAsync("caravan", 30, CancellationToken.None);

        Assert.Equal(SemanticSearchState.Unavailable, response.Semantic);
        Assert.Equal("Trade Routes of East Africa", response.Results[0].Title);
    }

    [Fact]
    public async Task OcrText_IsLabelledAndBothLanternBooksAreFound()
    {
        using SyntheticCorpusCatalogue corpus = SyntheticCorpusCatalogue.Create();
        corpus.AddPamphletOcrText();

        UnifiedSearchResponse response = await new UnifiedSearchService(corpus.Context, new FtsIndexService(corpus.Context))
            .SearchAsync("amber library lantern", 30, CancellationToken.None);

        Assert.Contains("The Lantern Keeper", response.Results.Take(2).Select(result => result.Title));
        UnifiedSearchResult pamphlet = Assert.Single(response.Results, result => result.BookId == SyntheticCorpusCatalogue.ScannedPamphletId);
        Assert.True(pamphlet.IsOcrText);
        Assert.Equal("Scanned Pamphlet (image only)", pamphlet.Title);
        Assert.Equal(0, pamphlet.PageJumpTarget?.PageIndex);
    }

    [Fact]
    public async Task Coverage_CountsSearchableAndScannedBooks()
    {
        SearchIndexCoverage coverage = await Service().GetCoverageAsync(CancellationToken.None);

        Assert.Equal(SyntheticCorpusCatalogue.WorkCount, coverage.TotalBooks);
        Assert.Equal(SyntheticCorpusCatalogue.WorkCount - 2, coverage.SearchableBooks);
        Assert.Equal(2, coverage.NeedOcrBooks);
    }

    [Theory]
    [InlineData("isbn:978-0-306-40615-7", "The Physics of Everyday Light")]
    [InlineData("9780131103627", "Algorithms Explained")]
    [InlineData("title:\"tropical ecology\"", "Introduction to Tropical Ecology")]
    [InlineData("author:namutebi sorting", "Algorithms Explained")]
    [InlineData("Ngugi", "Hadithi za Jioni: Évening Tales")]
    [InlineData("untitled_scan", "untitled_scan_0042")]
    [InlineData("Algo Expl", "Algorithms Explained")]
    public async Task StructuredAndPrefixQueries_FindTheBook(string query, string expected)
    {
        UnifiedSearchResponse response = await Service().SearchAsync(query, 30, CancellationToken.None);

        Assert.True(response.Results.Count > 0 && response.Results[0].Title == expected,
            $"'{query}' → [{string.Join(" | ", response.Results.Take(3).Select(result => result.Title))}]");
    }

    [Fact]
    public async Task Exclusion_RemovesMatchingBooks()
    {
        UnifiedSearchResponse all = await Service().SearchAsync("lantern", 30, CancellationToken.None);
        UnifiedSearchResponse excluded = await Service().SearchAsync("lantern -keeper", 30, CancellationToken.None);

        Assert.Contains(all.Results, result => result.Title == "The Lantern Keeper");
        Assert.DoesNotContain(excluded.Results, result => result.Title == "The Lantern Keeper");
    }

    [Fact]
    public async Task Typo_IsReportedAsTypoTolerant()
    {
        UnifiedSearchResponse response = await Service().SearchAsync("algoritms", 30, CancellationToken.None);

        Assert.True(response.UsedTypoTolerance);
        Assert.True(response.Results[0].Matches[0].IsFuzzy);
        Assert.Equal("Algorithms Explained", response.Results[0].CorrectionSuggestion);
    }

    [Theory]
    [InlineData("\"")]
    [InlineData("*")]
    [InlineData("NEAR(")]
    [InlineData(":")]
    [InlineData("title:")]
    [InlineData("\"unbalanced")]
    [InlineData("-")]
    [InlineData("AND OR NOT")]
    [InlineData("^col:x")]
    public async Task HostileInput_NeverThrows(string query)
    {
        UnifiedSearchResponse response = await Service().SearchAsync(query, 30, CancellationToken.None);

        Assert.NotNull(response.Results);
    }

    [Fact]
    public async Task LongQuery_IsTruncatedWithNotice()
    {
        UnifiedSearchResponse response = await Service().SearchAsync(string.Join(' ', Enumerable.Repeat("lantern", 100)), 30, CancellationToken.None);

        Assert.True(response.Query.WasTruncated);
    }

    [Fact]
    public async Task SlowProviderProbe_DoesNotBlockKeywordSearch()
    {
        var clock = Stopwatch.StartNew();
        UnifiedSearchResponse response = await Service(new SemanticStub(), new Provider(true, TimeSpan.FromSeconds(2)))
            .SearchAsync("caravan", 30, CancellationToken.None);

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1.5), $"took {clock.Elapsed}");
        Assert.Equal(SemanticSearchState.Unavailable, response.Semantic);
        Assert.NotEmpty(response.Results);
    }

    private UnifiedSearchService Service(ISemanticSearchService? semantic = null, IOllamaEmbeddingProvider? provider = null) =>
        new(_corpus.Context, new FtsIndexService(_corpus.Context), semantic, provider ?? new Provider(false));

    /// <summary>One read-only corpus shared by the tests in this class.</summary>
    public sealed class CorpusFixture : IDisposable
    {
        internal SyntheticCorpusCatalogue Corpus { get; } = SyntheticCorpusCatalogue.Create();

        public void Dispose() => Corpus.Dispose();
    }

    private sealed class Provider(bool available, TimeSpan delay = default) : IOllamaEmbeddingProvider
    {
        public string ProviderKey => "ollama";

        public bool IsLocalOnly => true;

        public Task<OllamaEmbeddingResult> EmbedAsync(string text, string modelName, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            return available;
        }
    }

    private sealed class SemanticStub : ISemanticSearchService
    {
        private readonly SemanticSearchResult[] _results;
        private readonly SemanticSearchAvailability _availability;
        private readonly bool _usedFallback;

        public SemanticStub(params SemanticSearchResult[] results)
            : this(results, SemanticSearchAvailability.Ready, false)
        {
        }

        public SemanticStub(SemanticSearchResult[] results, SemanticSearchAvailability availability, bool usedFallback)
        {
            _results = results;
            _availability = availability;
            _usedFallback = usedFallback;
        }

        public Task<SemanticSearchResponse> SearchAsync(string queryText, int maxResults, CancellationToken cancellationToken) =>
            Task.FromResult(new SemanticSearchResponse(false, _usedFallback, _results, _availability));
    }

    private sealed class ThrowingSemantic : ISemanticSearchService
    {
        public Task<SemanticSearchResponse> SearchAsync(string queryText, int maxResults, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("embedding failed");
    }
}
