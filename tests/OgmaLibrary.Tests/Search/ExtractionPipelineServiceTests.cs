using System.Data.Common;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Infrastructure.Search;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Phase 10 WP3 chunking and extraction pipeline tests.
/// </summary>
public sealed class ExtractionPipelineServiceTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public ExtractionPipelineServiceTests()
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
    public void SearchChunker_Uses512TokenChunksWith64TokenOverlap()
    {
        var chunker = new SearchChunker();
        string text = string.Join(' ', Enumerable.Range(0, 600).Select(i => $"t{i:000}"));

        IReadOnlyList<SearchChunkRecord> chunks = chunker.Chunk(
            "P10CHUNKBOOK000000000001",
            SearchChunkSource.Page,
            text,
            startingChunkIndex: 0,
            DateTimeOffset.UtcNow);

        Assert.Equal(2, chunks.Count);
        Assert.Equal(512, chunks[0].TokenCount);
        Assert.Equal(152, chunks[1].TokenCount);
        Assert.EndsWith("t511", chunks[0].Text, StringComparison.Ordinal);
        Assert.StartsWith("t448 ", chunks[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractionPipeline_IndexBook_PersistsPagesAndImplementedSources()
    {
        string bookId = SeedBookWithSearchSources();
        var rendererFactory = new FakeRendererFactory([
            new TextLayer(
                0,
                [Word("introductory"), Word("chapter"), Word("rarepagekeyword")],
                ExtractionQuality.Full),
            new TextLayer(1, [], ExtractionQuality.Scanned),
        ]);
        ExtractionPipelineService service = CreateService(rendererFactory);

        ExtractionBookResult result = await service.IndexBookAsync(bookId, CancellationToken.None);
        IReadOnlyList<SearchChunkRecord> chunks = await new SearchChunkRepository(_context)
            .ListForBookAsync(bookId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.PagesProcessed);
        Assert.Equal(0, result.PagesSkipped);
        Assert.Equal(0, result.FailedPages);
        Assert.Equal((int)SearchBookIndexStatus.Indexed, _context.Books.Single(b => b.BookId == bookId).IndexStatus);
        Assert.Equal(2, _context.ExtractedPages.Count(p => p.BookId == bookId));
        Assert.Contains(chunks, c => c.Source == SearchChunkSource.Page && c.Text.Contains("rarepagekeyword", StringComparison.Ordinal));
        Assert.Contains(chunks, c => c.Source == SearchChunkSource.Note && c.Text.Contains("schoollibrarynote", StringComparison.Ordinal));
        Assert.Contains(chunks, c => c.Source == SearchChunkSource.Tag && c.Text.Contains("classroom", StringComparison.Ordinal));
        Assert.Contains(chunks, c => c.Source == SearchChunkSource.Description && c.Text.Contains("awardreadydescription", StringComparison.Ordinal));
        Assert.Equal(1, CountFtsMatches("rarepagekeyword"));
        Assert.Equal(1, CountFtsMatches("schoollibrarynote"));
        Assert.Equal(1, CountFtsMatches("awardreadydescription"));
    }

    [Fact]
    public async Task ExtractionPipeline_RerunWithSameHash_SkipsPagesAndAvoidsDuplicateChunks()
    {
        string bookId = SeedBookWithSearchSources();
        var rendererFactory = new FakeRendererFactory([
            new TextLayer(0, [Word("stable"), Word("page"), Word("keyword")], ExtractionQuality.Full),
            new TextLayer(1, [Word("second"), Word("page")], ExtractionQuality.Partial),
        ]);
        ExtractionPipelineService service = CreateService(rendererFactory);

        ExtractionBookResult first = await service.IndexBookAsync(bookId, CancellationToken.None);
        int firstChunkCount = _context.SearchChunks.Count(c => c.BookId == bookId);
        int firstPageCount = _context.ExtractedPages.Count(p => p.BookId == bookId);
        ExtractionBookResult second = await service.IndexBookAsync(bookId, CancellationToken.None);

        Assert.Equal(2, first.PagesProcessed);
        Assert.Equal(2, second.PagesSkipped);
        Assert.Equal(0, second.PagesProcessed);
        Assert.Equal(firstPageCount, _context.ExtractedPages.Count(p => p.BookId == bookId));
        Assert.Equal(firstChunkCount, _context.SearchChunks.Count(c => c.BookId == bookId));
        Assert.Equal(2, rendererFactory.ExtractCalls);
    }

    [Fact]
    public async Task ExtractionPipeline_PageFailure_RecordsFailedPageAndJobThenContinues()
    {
        string bookId = SeedBookWithSearchSources();
        var rendererFactory = new FakeRendererFactory(
            [
                new TextLayer(0, [Word("healthy"), Word("page"), Word("text")], ExtractionQuality.Full),
                new TextLayer(1, [Word("unused")], ExtractionQuality.Full),
            ],
            failingPages: [1]);
        ExtractionPipelineService service = CreateService(rendererFactory);

        ExtractionBookResult result = await service.IndexBookAsync(bookId, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(2, result.PagesProcessed);
        Assert.Equal(1, result.FailedPages);
        Assert.Equal((int)SearchBookIndexStatus.Failed, _context.Books.Single(b => b.BookId == bookId).IndexStatus);
        Assert.Contains(_context.ExtractedPages, page =>
            page.BookId == bookId &&
            page.PageNumber == 1 &&
            page.ExtractionQuality == (int)SearchExtractionQuality.Failed);
        Assert.Contains(_context.Jobs, job =>
            job.BookId == bookId &&
            job.JobType == "ExtractionFailed" &&
            job.Status == 3 &&
            job.Payload != null &&
            job.Payload.Contains("\"pageIndex\":1", StringComparison.Ordinal));
        Assert.Equal(1, CountFtsMatches("healthy"));
    }

    [Fact]
    public async Task ExtractionPipeline_CancelledMidBook_ResetsBookForRecovery()
    {
        string bookId = SeedBookWithSearchSources();
        var rendererFactory = new FakeRendererFactory(
            [
                new TextLayer(0, [Word("before"), Word("cancel")], ExtractionQuality.Full),
                new TextLayer(1, [Word("unused")], ExtractionQuality.Full),
            ],
            cancellingPages: [1]);
        ExtractionPipelineService service = CreateService(rendererFactory);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.IndexBookAsync(bookId, CancellationToken.None));

        Assert.Equal((int)SearchBookIndexStatus.NotIndexed, _context.Books.Single(b => b.BookId == bookId).IndexStatus);
    }

    [Fact]
    public async Task ExtractionPipeline_IndexNextBatch_FindsStaleAndPendingBooks()
    {
        string firstBook = SeedBook("P10BATCHBOOK00000000001", "hash-a");
        string secondBook = SeedBook("P10BATCHBOOK00000000002", "hash-b");
        _context.ExtractedPages.Add(new ExtractedPageRow
        {
            BookId = secondBook,
            PageNumber = 0,
            TextContent = "old text",
            ExtractionQuality = (int)SearchExtractionQuality.Full,
            WordCount = 2,
            ContentHash = "old-hash",
            ExtractionUtc = DateTimeOffset.UtcNow,
        });
        _context.Books.Single(b => b.BookId == secondBook).IndexStatus = (int)SearchBookIndexStatus.Indexed;
        _context.SaveChanges();

        var rendererFactory = new FakeRendererFactory([
            new TextLayer(0, [Word("batch"), Word("indexed")], ExtractionQuality.Full),
        ]);
        ExtractionPipelineService service = CreateService(rendererFactory);

        ExtractionBatchResult result = await service.IndexNextBatchAsync(10, CancellationToken.None);

        Assert.Equal(2, result.BooksAttempted);
        Assert.Equal(2, result.BooksIndexed);
        Assert.All([firstBook, secondBook], bookId =>
            Assert.Equal((int)SearchBookIndexStatus.Indexed, _context.Books.Single(b => b.BookId == bookId).IndexStatus));
    }

    private ExtractionPipelineService CreateService(FakeRendererFactory rendererFactory)
    {
        var chunker = new SearchChunker();
        return new ExtractionPipelineService(
            _context,
            new FakeBookFileLocator(),
            rendererFactory,
            new ExtractedTextStore(_context),
            new SearchChunkRepository(_context),
            chunker);
    }

    private string SeedBookWithSearchSources()
    {
        string bookId = SeedBook("P10PIPEBOOK000000000001", new string('a', 64));
        _context.AnnotationsV2.Add(new AnnotationV2Row
        {
            AnnotationId = "P10NOTE000000000000000001",
            BookId = bookId,
            Type = 1,
            RegionsJson = "[]",
            NoteText = "schoollibrarynote for teachers",
            CreatedUtc = DateTimeOffset.UtcNow,
            ModifiedUtc = DateTimeOffset.UtcNow,
        });
        _context.BookMetadataFields.AddRange(
            new BookMetadataFieldRow
            {
                BookId = bookId,
                FieldName = "Tags",
                Value = "classroom interactive reading",
                Source = "Test",
            },
            new BookMetadataFieldRow
            {
                BookId = bookId,
                FieldName = "Description",
                Value = "awardreadydescription for open education",
                Source = "Test",
            });
        _context.Shelves.Add(new ShelfRow
        {
            ShelfId = "P10SHELF000000000000001",
            Name = "school collection",
            ShelfType = 0,
            DisplayOrder = 0,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        _context.ShelfBooks.Add(new ShelfBookRow
        {
            ShelfId = "P10SHELF000000000000001",
            BookId = bookId,
            AddedUtc = DateTimeOffset.UtcNow,
            DisplayOrder = 0,
        });
        _context.SaveChanges();
        return bookId;
    }

    private string SeedBook(string bookId, string hash)
    {
        _context.Books.Add(new BookRow
        {
            BookId = bookId,
            Title = "Phase 10 Pipeline Book",
            Sha256Hash = hash,
            Status = 0,
            IndexStatus = (int)SearchBookIndexStatus.NotIndexed,
        });
        _context.SaveChanges();
        return bookId;
    }

    private int CountFtsMatches(string query)
    {
        _context.Database.OpenConnection();
        using DbCommand command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM SearchFts5 WHERE SearchFts5 MATCH $query;";
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "$query";
        parameter.Value = query;
        command.Parameters.Add(parameter);
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static TextWord Word(string text) => new(text, 0, 0, 0.1, 0.1);

    private sealed class FakeBookFileLocator : IBookFileLocator
    {
        public Task<string?> LocateAsync(string bookId, CancellationToken ct) =>
            Task.FromResult<string?>($"C:\\fake\\{bookId}.pdf");
    }

    private sealed class FakeRendererFactory : IPdfRendererFactory
    {
        private readonly IReadOnlyList<TextLayer> _pages;
        private readonly HashSet<int> _failingPages;

        private readonly HashSet<int> _cancellingPages;

        public FakeRendererFactory(
            IReadOnlyList<TextLayer> pages,
            IReadOnlyCollection<int>? failingPages = null,
            IReadOnlyCollection<int>? cancellingPages = null)
        {
            _pages = pages;
            _failingPages = failingPages?.ToHashSet() ?? [];
            _cancellingPages = cancellingPages?.ToHashSet() ?? [];
        }

        public int ExtractCalls { get; private set; }

        public IPdfRenderer Open(string filePath) => new FakeRenderer(this);

        private sealed class FakeRenderer : IPdfRenderer
        {
            private readonly FakeRendererFactory _factory;

            public FakeRenderer(FakeRendererFactory factory)
            {
                _factory = factory;
            }

            public int PageCount => _factory._pages.Count;

            public void Dispose()
            {
            }

            public Task<RenderResult> RenderPageAsync(int pageIndex, RenderRequest request, CancellationToken ct) =>
                Task.FromResult(new RenderResult([], 100, 100, pageIndex));

            public int GetPageRotationDegrees(int pageIndex) => 0;

            public TextLayer ExtractTextLayer(int pageIndex)
            {
                _factory.ExtractCalls++;
                if (_factory._failingPages.Contains(pageIndex))
                {
                    throw new InvalidOperationException("fixture extraction failure");
                }

                if (_factory._cancellingPages.Contains(pageIndex))
                {
                    throw new OperationCanceledException();
                }

                return _factory._pages[pageIndex];
            }
        }
    }
}
