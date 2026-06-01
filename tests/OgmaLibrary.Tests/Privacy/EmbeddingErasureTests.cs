using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Search;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Privacy;

/// <summary>
/// Phase 11 WP6 privacy erasure tests for locally derived embeddings.
/// </summary>
public sealed class EmbeddingErasureTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public EmbeddingErasureTests()
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
    public async Task EmbeddingErasure_AllRowsDeleted_AndAuditEventPresent()
    {
        SeedEmbeddings(bookCount: 4, vectorsPerBook: 25);
        var service = new EmbeddingErasureService(_context);

        EmbeddingErasureResult result = await service.EraseAllAsync(CancellationToken.None);
        _context.ChangeTracker.Clear();

        Assert.Equal(100, result.VectorsErased);
        Assert.Equal(4, result.BooksReset);
        Assert.Equal(0, _context.EmbeddingVectors.Count());
        Assert.All(_context.Books, book =>
            Assert.Equal((int)SearchEmbeddingStatus.NotEmbedded, book.EmbeddingStatus));
        AuditEventRow audit = Assert.Single(_context.AuditEvents);
        Assert.Equal(EmbeddingErasureService.AuditEventType, audit.EventType);
        Assert.Equal("EmbeddingVectors", audit.EntityType);
        Assert.Contains("\"vectorsErased\":100", audit.AfterJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmbeddingErasure_PipelineCanRequeueAfterErasure()
    {
        SeedEmbeddings(bookCount: 1, vectorsPerBook: 3);
        var service = new EmbeddingErasureService(_context);

        _ = await service.EraseAllAsync(CancellationToken.None);
        _context.ChangeTracker.Clear();

        bool hasPendingChunks = _context.SearchChunks.Any(chunk =>
            !_context.EmbeddingVectors.Any(vector => vector.ChunkId == chunk.ChunkId));
        Assert.True(hasPendingChunks);
        Assert.Equal(
            (int)SearchEmbeddingStatus.NotEmbedded,
            _context.Books.Single().EmbeddingStatus);
    }

    private void SeedEmbeddings(int bookCount, int vectorsPerBook)
    {
        for (int bookIndex = 0; bookIndex < bookCount; bookIndex++)
        {
            string bookId = $"P11ERASE{bookIndex:00000000000000000}";
            _context.Books.Add(new BookRow
            {
                BookId = bookId,
                Title = $"Erasure Test {bookIndex}",
                Status = 0,
                IndexStatus = (int)SearchBookIndexStatus.Indexed,
                EmbeddingStatus = (int)SearchEmbeddingStatus.Embedded,
            });

            for (int vectorIndex = 0; vectorIndex < vectorsPerBook; vectorIndex++)
            {
                var chunk = new SearchChunkRow
                {
                    BookId = bookId,
                    ChunkIndex = vectorIndex,
                    ChunkText = $"chunk {bookIndex} {vectorIndex}",
                    Source = (int)SearchChunkSource.Page,
                    TokenCount = 3,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                };
                _context.SearchChunks.Add(chunk);
                _context.EmbeddingVectors.Add(new EmbeddingVectorRow
                {
                    Chunk = chunk,
                    ModelName = EmbeddingGenerationService.DefaultModelName,
                    ModelVersion = EmbeddingGenerationService.DefaultModelVersion,
                    DimensionCount = 2,
                    VectorBlob = Serialize([1.0f, 0.0f]),
                    GeneratedAtUtc = DateTimeOffset.UtcNow,
                });
            }
        }

        _context.SaveChanges();
    }

    private static byte[] Serialize(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
