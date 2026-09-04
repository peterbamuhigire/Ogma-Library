using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Infrastructure.Search;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Deterministic local quality gate for semantic retrieval over a small,
/// intentionally varied concept fixture. Reference-corpus claims remain
/// outside this test's scope.
/// </summary>
public sealed class Phase26RepresentativeRetrievalTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public Phase26RepresentativeRetrievalTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.Migrate();
    }

    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }

    [Fact]
    public async Task SemanticRetrieval_LocalConceptFixture_MeetsPerfectTopThreeJudgmentGate()
    {
        var queryVectors = new Dictionary<string, float[]>(StringComparer.Ordinal)
        {
            ["governance"] = [1f, 0f],
            ["pedagogy"] = [0f, 1f],
            ["systems"] = [0.7f, 0.7f],
            ["history"] = [-1f, 0f],
        };
        var judgments = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["governance"] = "P26-BOOK-GOVERNANCE",
            ["pedagogy"] = "P26-BOOK-PEDAGOGY",
            ["systems"] = "P26-BOOK-SYSTEMS",
            ["history"] = "P26-BOOK-HISTORY",
        };

        var vectors = new Dictionary<string, float[]>(StringComparer.Ordinal)
        {
            ["P26-BOOK-GOVERNANCE"] = [1f, 0f],
            ["P26-BOOK-PEDAGOGY"] = [0f, 1f],
            ["P26-BOOK-SYSTEMS"] = [0.7f, 0.7f],
            ["P26-BOOK-HISTORY"] = [-1f, 0f],
            ["P26-BOOK-COOKING"] = [0.2f, 0.05f],
            ["P26-BOOK-MUSIC"] = [0f, -1f],
        };
        var repository = new EmbeddingVectorRepository(_context);
        foreach ((string bookId, float[] vector) in vectors)
        {
            long chunkId = SeedBook(bookId);
            await repository.CreateAsync(
                new EmbeddingVectorRecord(
                    0,
                    chunkId,
                    EmbeddingGenerationService.DefaultModelName,
                    EmbeddingGenerationService.DefaultModelVersion,
                    vector,
                    vector.Length,
                    DateTimeOffset.UtcNow,
                    new string('a', 64)),
                CancellationToken.None);
        }

        var service = new SemanticSearchService(
            _context,
            new FixtureEmbeddingProvider(queryVectors),
            new EmptyExactSearch());
        var cases = new List<SearchEvaluationCase>(judgments.Count);
        foreach ((string query, string relevantBookId) in judgments)
        {
            SemanticSearchResponse response = await service.SearchAsync(query, 3, CancellationToken.None);
            cases.Add(new SearchEvaluationCase(
                query,
                query,
                response.Results.Select(result => result.BookId).ToArray(),
                new HashSet<string>([relevantBookId], StringComparer.Ordinal),
                k: 3));
        }

        SearchEvaluationReport report = SearchOfflineEvaluator.Evaluate(cases);

        Assert.Equal(4, report.CaseCount);
        Assert.Equal(1.0, report.RecallAtK);
        Assert.All(report.Cases, result => Assert.True(
            result.MeanReciprocalRank == 1.0,
            $"Expected {result.QueryId} to rank its judged book first; actual order: {string.Join(",", result.RankedBookIds)}"));
        Assert.Equal(1.0, report.MeanReciprocalRank);
        Assert.Equal(1.0, report.NdcgAtK);
    }

    private long SeedBook(string bookId)
    {
        _context.Books.Add(new BookRow
        {
            BookId = bookId,
            Title = bookId,
            Status = 0,
            IndexStatus = (int)SearchBookIndexStatus.Indexed,
            EmbeddingStatus = (int)SearchEmbeddingStatus.NotEmbedded,
        });
        var chunk = new SearchChunkRow
        {
            BookId = bookId,
            ChunkIndex = 0,
            ChunkText = $"fixture text for {bookId}",
            Source = (int)SearchChunkSource.Page,
            TokenCount = 4,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        _context.SearchChunks.Add(chunk);
        _context.SaveChanges();
        return chunk.ChunkId;
    }

    private sealed class FixtureEmbeddingProvider(
        IReadOnlyDictionary<string, float[]> vectors) : IOllamaEmbeddingProvider
    {
        public string ProviderKey => "ollama";

        public bool IsLocalOnly => true;

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<OllamaEmbeddingResult> EmbedAsync(
            string text,
            string modelName,
            CancellationToken cancellationToken) =>
            Task.FromResult(new OllamaEmbeddingResult(
                modelName,
                EmbeddingGenerationService.DefaultModelVersion,
                [.. vectors[text]]));
    }

    private sealed class EmptyExactSearch : ICombinedSearchService
    {
        public Task<IReadOnlyList<CombinedSearchResult>> SearchAsync(
            string? query,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CombinedSearchResult>>([]);
    }
}
