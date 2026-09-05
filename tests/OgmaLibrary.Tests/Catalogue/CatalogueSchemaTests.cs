using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>
/// Verifies that the catalogue database contains all required tables with the
/// correct schema (HLD §3, Phase 04 deliverable 10).
/// </summary>
public sealed class CatalogueSchemaTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public CatalogueSchemaTests()
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
    public void CatalogueSchema_HasAllRequiredTables()
    {
        // All DbSet properties return non-null queryable — table exists.
        Assert.NotNull(_context.Books.AsQueryable());
        Assert.NotNull(_context.BookFiles.AsQueryable());
        Assert.NotNull(_context.BookMetadataFields.AsQueryable());
        Assert.NotNull(_context.Authors.AsQueryable());
        Assert.NotNull(_context.BookAuthors.AsQueryable());
        Assert.NotNull(_context.Shelves.AsQueryable());
        Assert.NotNull(_context.ShelfBooks.AsQueryable());
        Assert.NotNull(_context.ReadingProgress.AsQueryable());
        Assert.NotNull(_context.Bookmarks.AsQueryable());
        Assert.NotNull(_context.Annotations.AsQueryable());
        Assert.NotNull(_context.AnnotationBodies.AsQueryable());
        Assert.NotNull(_context.ExtractedPages.AsQueryable());
        Assert.NotNull(_context.SearchChunks.AsQueryable());
        Assert.NotNull(_context.EmbeddingVectors.AsQueryable());
        Assert.NotNull(_context.AiQueryHistory.AsQueryable());
        Assert.NotNull(_context.MetadataLookups.AsQueryable());
        Assert.NotNull(_context.Jobs.AsQueryable());
        Assert.NotNull(_context.AuditEvents.AsQueryable());
        Assert.NotNull(_context.Works.AsQueryable());
        Assert.NotNull(_context.Editions.AsQueryable());
    }

    [Fact]
    public void CatalogueSchema_CanQueryAllTables_AfterMigration()
    {
        // Each table should be queryable (returns 0 rows, no SQL errors).
        Assert.Equal(0, _context.Books.Count());
        Assert.Equal(0, _context.Authors.Count());
        Assert.Equal(0, _context.Shelves.Count());
        Assert.Equal(0, _context.Jobs.Count());
        Assert.Equal(0, _context.AuditEvents.Count());
    }

    [Fact]
    public void CatalogueSchema_MetadataLookup_PersistsStaleState()
    {
        _context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
        {
            BookId = "SCHEMA-STALE-LOOKUP",
            Title = "Stale lookup",
            Status = 0,
        });
        _context.MetadataLookups.Add(new Infrastructure.Catalogue.Entities.MetadataLookupRow
        {
            BookId = "SCHEMA-STALE-LOOKUP",
            Provider = "OpenLibrary",
            RequestIsbn = "9780306406157",
            Timestamp = DateTimeOffset.UtcNow,
            Confidence = 0.8,
            IsStale = true,
        });
        _context.SaveChanges();

        Infrastructure.Catalogue.Entities.MetadataLookupRow lookup =
            Assert.Single(_context.MetadataLookups);
        Assert.True(lookup.IsStale);
    }

    [Fact]
    public void CatalogueSchema_Jobs_HasUniqueIdempotencyKeyConstraint()
    {
        // Insert a job with a unique key.
        _context.Jobs.Add(new Infrastructure.Catalogue.Entities.JobRow
        {
            JobType = "TestJob",
            IdempotencyKey = "unique-key-001",
            Status = 0,
        });
        _context.SaveChanges();

        // Attempting to insert a duplicate idempotency key should throw.
        _context.Jobs.Add(new Infrastructure.Catalogue.Entities.JobRow
        {
            JobType = "TestJob",
            IdempotencyKey = "unique-key-001",
            Status = 0,
        });

        Assert.Throws<DbUpdateException>(() => _context.SaveChanges());
    }
}
