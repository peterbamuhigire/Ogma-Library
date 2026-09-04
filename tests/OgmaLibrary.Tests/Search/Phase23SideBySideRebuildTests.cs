using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Infrastructure.Search;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Search;

/// <summary>Phase 23 active-index availability and staging-promotion tests.</summary>
public sealed class Phase23SideBySideRebuildTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public Phase23SideBySideRebuildTests()
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
    public async Task Rebuild_KeepsActiveSearchReadableUntilAtomicPromotion()
    {
        const string bookId = "PH23SIDEBYSIDE00000000000001";
        _context.Books.Add(new BookRow
        {
            BookId = bookId,
            Title = "Side-by-side rebuild",
            Status = 0,
            IndexStatus = (int)SearchBookIndexStatus.Indexed,
        });
        await _context.SaveChangesAsync();
        var chunks = new SearchChunkRepository(_context);
        await chunks.ReplaceForBookAsync(bookId, SearchChunkSource.Page, [
            new SearchChunkRecord(
                0, bookId, null, null, 0, "old active passage", 3,
                SearchChunkSource.Page, DateTimeOffset.UtcNow),
        ], CancellationToken.None);

        var fts = new FtsIndexService(_context);
        var pipeline = new StagedPipeline(_context, fts, bookId);
        using var manager = new IndexManagerService(_context, pipeline, fts);

        IndexRebuildResult result = await manager.RebuildAsync(CancellationToken.None);

        Assert.True(result.Completed, result.ErrorMessage);
        Assert.True(pipeline.ActiveIndexWasReadable);
        Assert.Empty(await fts.SearchAsync("old active", 10, CancellationToken.None));
        Assert.Contains(await fts.SearchAsync("new staged", 10, CancellationToken.None),
            hit => hit.BookId == bookId);
        Assert.All(
            await _context.SearchChunks.AsNoTracking().ToListAsync(),
            chunk => Assert.Equal("fts5-v1", chunk.IndexVersion));
    }

    private sealed class StagedPipeline : IExtractionPipelineService, IStagedExtractionPipelineService
    {
        private readonly CatalogueDbContext _context;
        private readonly IFtsIndexService _fts;
        private readonly string _bookId;
        private bool _written;

        public StagedPipeline(CatalogueDbContext context, IFtsIndexService fts, string bookId)
        {
            _context = context;
            _fts = fts;
            _bookId = bookId;
        }

        public bool ActiveIndexWasReadable { get; private set; }

        public Task<ExtractionBookResult> IndexBookAsync(string bookId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ExtractionBatchResult> IndexNextBatchAsync(
            int maxBooks,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async Task<ExtractionBatchResult> IndexNextBatchAsync(
            int maxBooks,
            string indexVersion,
            CancellationToken cancellationToken)
        {
            if (_written)
            {
                return new ExtractionBatchResult(0, 0, 0, 0, 0, 0, 0);
            }

            await new SearchChunkRepository(_context).ReplaceForBookAsync(
                _bookId,
                SearchChunkSource.Page,
                [new SearchChunkRecord(
                    0,
                    _bookId,
                    null,
                    null,
                    0,
                    "new staged passage",
                    3,
                    SearchChunkSource.Page,
                    DateTimeOffset.UtcNow,
                    IndexVersion: indexVersion)],
                cancellationToken,
                indexVersion);
            ActiveIndexWasReadable = (await _fts.SearchAsync(
                "old active", 10, cancellationToken)).Any(hit => hit.BookId == _bookId);
            _written = true;
            return new ExtractionBatchResult(1, 1, 0, 0, 0, 0, 1);
        }
    }
}
