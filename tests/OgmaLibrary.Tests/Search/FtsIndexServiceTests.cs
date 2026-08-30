using System.Diagnostics;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Infrastructure.Search;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Phase 10 WP4 FTS5 query, snippet, integrity, and performance tests.
/// </summary>
public sealed class FtsIndexServiceTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public FtsIndexServiceTests()
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
    public async Task FtsIndex_Search_ReturnsRankedSnippetWithBookMetadata()
    {
        string firstBook = SeedBook("P10FTSBOOK000000000001", "Ogma Search Handbook", "Ada Indexer");
        string secondBook = SeedBook("P10FTSBOOK000000000002", "Quiet Library Notes", "Grace Reader");
        await SaveChunksAsync(firstBook, SearchChunkSource.Page, "introductory chapter rareftskeyword classroom reading", "other page text");
        await SaveChunksAsync(secondBook, SearchChunkSource.Page, "rareftskeyword appears once");
        var service = new FtsIndexService(_context);

        IReadOnlyList<FtsSearchResult> results = await service.SearchAsync(
            "rareftskeyword",
            limit: 10,
            CancellationToken.None);

        Assert.Contains(results, result => result.BookId == secondBook);
        FtsSearchResult firstBookResult = Assert.Single(results, result => result.BookId == firstBook);
        Assert.Equal("Ogma Search Handbook", firstBookResult.Title);
        Assert.Equal("Ada Indexer", firstBookResult.Author);
        Assert.Equal(SearchChunkSource.Page, firstBookResult.Source);
        Assert.Contains("<b>rareftskeyword</b>", firstBookResult.Snippet, StringComparison.OrdinalIgnoreCase);
        Assert.True(results[0].Score >= results[^1].Score);
    }

    [Fact]
    public async Task FtsIndex_Search_MatchesDiacriticsAndMultipleSources()
    {
        string bookId = SeedBook("P10FTSBOOK000000000003", "French Classroom Reader", "Marie Curie");
        await SaveChunksAsync(bookId, SearchChunkSource.Page, "cafe culture and education");
        await SaveChunksAsync(bookId, SearchChunkSource.Note, "teacher note with noteftsmarker");
        await SaveChunksAsync(bookId, SearchChunkSource.Tag, "bilingual classroom tagftsmarker");
        await SaveChunksAsync(bookId, SearchChunkSource.Description, "descriptionftsmarker summary");
        var service = new FtsIndexService(_context);

        IReadOnlyList<FtsSearchResult> cafe = await service.SearchAsync("cafe", 10, CancellationToken.None);
        IReadOnlyList<FtsSearchResult> note = await service.SearchAsync("noteftsmarker", 10, CancellationToken.None);
        IReadOnlyList<FtsSearchResult> tag = await service.SearchAsync("tagftsmarker", 10, CancellationToken.None);
        IReadOnlyList<FtsSearchResult> description = await service.SearchAsync("descriptionftsmarker", 10, CancellationToken.None);

        Assert.Contains(cafe, result => result.BookId == bookId);
        Assert.Contains(note, result => result.Source == SearchChunkSource.Note);
        Assert.Contains(tag, result => result.Source == SearchChunkSource.Tag);
        Assert.Contains(description, result => result.Source == SearchChunkSource.Description);
    }

    [Fact]
    public async Task FtsIndex_Search_SanitizesInvalidInputAndIntegrityCheckPasses()
    {
        string bookId = SeedBook("P10FTSBOOK000000000004", "Integrity Book", null);
        await SaveChunksAsync(bookId, SearchChunkSource.Page, "integrity marker text");
        var service = new FtsIndexService(_context);

        IReadOnlyList<FtsSearchResult> empty = await service.SearchAsync("***", 10, CancellationToken.None);
        FtsIntegrityResult integrity = await service.CheckIntegrityAsync(CancellationToken.None);

        Assert.Empty(empty);
        Assert.True(integrity.IsHealthy, integrity.ErrorMessage);
    }

    [Fact]
    public async Task FtsIndex_CleanupStaleArtifact_RemovesChunksAndPreservesIntegrity()
    {
        string bookId = SeedBook("P23STALEARTIFACT0000001", "Stale Artifact Book", null);
        _context.Books.Single(book => book.BookId == bookId).Sha256Hash = "current-hash";
        var artifact = new ExtractionArtifactRow
        {
            BookId = bookId,
            ContentHash = "previous-hash",
            ExtractorVersion = "pdf-text-v1",
            Status = (int)ExtractionArtifactStatus.Completed,
            PagesProcessed = 1,
            ManifestHash = new string('a', 64),
            CreatedUtc = DateTimeOffset.UtcNow,
            CompletedUtc = DateTimeOffset.UtcNow,
        };
        _context.ExtractionArtifacts.Add(artifact);
        _context.SaveChanges();
        await SaveChunksAsync(
            bookId,
            SearchChunkSource.Page,
            "stale artifact marker");
        SearchChunkRow chunk = _context.SearchChunks.Single(row => row.BookId == bookId);
        chunk.ExtractionArtifactId = artifact.ExtractionArtifactId;
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        var service = new FtsIndexService(_context);
        FtsCleanupResult cleanup = await service.CleanupStaleAsync(CancellationToken.None);

        Assert.Equal(1, cleanup.RemovedChunkCount);
        Assert.True(cleanup.IntegrityHealthy, cleanup.ErrorMessage);
        Assert.Empty(await service.SearchAsync("stale", 10, CancellationToken.None));
    }

    [Fact]
    public async Task CombinedSearch_DeduplicatesMetadataAndFtsHitsByBook()
    {
        string bookId = SeedBook("P10FTSBOOK000000000005", "Combined Search Atlas", "Nora Merge");
        _context.BookMetadataFields.Add(new BookMetadataFieldRow
        {
            BookId = bookId,
            FieldName = "Tags",
            Value = "combinedtoken",
            Source = "Test",
        });
        _context.SaveChanges();
        await SaveChunksAsync(bookId, SearchChunkSource.Page, "combinedtoken appears in full text");
        var combined = new CombinedSearchService(
            new MetadataSearchService(_context),
            new FtsIndexService(_context));

        IReadOnlyList<CombinedSearchResult> results = await combined.SearchAsync(
            "combinedtoken",
            limit: 10,
            CancellationToken.None);

        CombinedSearchResult result = Assert.Single(results);
        Assert.Equal(bookId, result.BookId);
        Assert.Contains("tag", result.MatchedFields);
        Assert.Contains("full-text:page", result.MatchedFields);
        Assert.Single(result.FtsHits);
    }

    [Fact]
    public async Task PerfBenchmark_FtsSearch_P95_LessThan500ms()
    {
        const int bookCount = 2000;
        const int queryCount = 50;
        var books = new List<BookRow>(bookCount);
        var chunks = new List<SearchChunkRow>(bookCount);
        for (int i = 0; i < bookCount; i++)
        {
            string bookId = $"P10FTSPERF{i:000000000000000}";
            books.Add(new BookRow
            {
                BookId = bookId,
                Title = $"Search Performance Book {i:0000}",
                Status = 0,
                IndexStatus = (int)SearchBookIndexStatus.Indexed,
            });
            chunks.Add(new SearchChunkRow
            {
                BookId = bookId,
                ChunkIndex = 0,
                ChunkText = $"school library performance marker{i % queryCount:00} shared corpus text book{i:0000}",
                Source = (int)SearchChunkSource.Page,
                TokenCount = 8,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
        }

        _context.Books.AddRange(books);
        _context.SearchChunks.AddRange(chunks);
        _context.SaveChanges();
        var service = new FtsIndexService(_context);

        _ = await service.SearchAsync("marker00", 25, CancellationToken.None);

        var elapsed = new List<double>(queryCount);
        for (int i = 0; i < queryCount; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            IReadOnlyList<FtsSearchResult> results = await service.SearchAsync(
                $"marker{i:00}",
                25,
                CancellationToken.None);
            stopwatch.Stop();
            Assert.NotEmpty(results);
            elapsed.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        double p95 = elapsed
            .OrderBy(value => value)
            .ElementAt((int)Math.Ceiling(queryCount * 0.95) - 1);
        Assert.True(p95 <= 500, $"Expected FTS P95 <= 500 ms, actual {p95:F2} ms");
    }

    private async Task SaveChunksAsync(
        string bookId,
        SearchChunkSource source,
        params string[] texts)
    {
        var repository = new SearchChunkRepository(_context);
        IReadOnlyList<SearchChunkRecord> chunks = texts
            .Select((text, index) => new SearchChunkRecord(
                Id: 0,
                BookId: bookId,
                ExtractedPageId: null,
                PageIndex: null,
                ChunkIndex: index,
                Text: text,
                TokenCount: SearchChunker.CountTokens(text),
                Source: source,
                CreatedAtUtc: DateTimeOffset.UtcNow))
            .ToList();

        await repository.ReplaceForBookAsync(bookId, source, chunks, CancellationToken.None);
    }

    private string SeedBook(string bookId, string title, string? authorName)
    {
        _context.Books.Add(new BookRow
        {
            BookId = bookId,
            Title = title,
            Status = 0,
            IndexStatus = (int)SearchBookIndexStatus.Indexed,
        });

        if (!string.IsNullOrWhiteSpace(authorName))
        {
            var author = new AuthorRow { NormalizedName = authorName };
            _context.Authors.Add(author);
            _context.BookAuthors.Add(new BookAuthorRow
            {
                BookId = bookId,
                Author = author,
                DisplayOrder = 0,
            });
        }

        _context.SaveChanges();
        return bookId;
    }
}
