using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>
/// Sept-23 Phase 06 migration rehearsal (T06.1, T06.4): a copy of an existing catalogue at the
/// Phase 05 schema, holding the audit's job table shape (an <c>ExtractionFailed</c> record
/// with RetryCount 48, embedding jobs "failed" for a missing provider), is migrated with the
/// production migrator; the verified backup restores the untouched previous state.
/// </summary>
public sealed class JobAccountingMigrationRehearsalTests : IDisposable
{
    private const string PreviousMigration = "20260925082519_Sept23Phase05LibraryRootsAndValidity";
    private readonly string _temp = Path.Combine(Path.GetTempPath(), $"ogma-p06-migration-{Guid.NewGuid():N}");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_temp, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    [Fact]
    public async Task JobAccountingMigration_MovesFailureRecords_AndParksProviderWaitingJobs()
    {
        string copy = Path.Combine(_temp, "copy", "catalogue.db");
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        await using (CatalogueDbContext legacy = Open(copy))
        {
            await legacy.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            string hash = new('a', 64);
            await legacy.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO Books (BookId, Title, Status, Sha256Hash) VALUES ('B1', 'Book', 0, {hash})");
            await legacy.Database.ExecuteSqlRawAsync(
                "INSERT INTO Jobs (JobType, IdempotencyKey, Status, BookId, Payload, RetryCount, ErrorMessage, CompletedUtc) VALUES " +
                "('ExtractionFailed', 'k1', 3, 'B1', '{{\"source\":\"search-extraction\",\"pageIndex\":3}}', 48, 'Search page extraction failed.', '2026-09-25 10:00:00+00:00'), " +
                "('ExtractionFailed', 'k2', 3, 'B1', '{{\"source\":\"search-extraction\"}}', 2, 'Search artifact preparation failed.', NULL), " +
                "('EmbeddingJob', 'k3', 3, 'B1', NULL, 3, 'The local embedding provider is unavailable.', NULL), " +
                "('ThumbnailGeneration', 'k4', 2, 'B1', NULL, 1, NULL, NULL)");
            await legacy.Database.ExecuteSqlRawAsync(
                "UPDATE Jobs SET FailureCode = 'embedding_provider_unavailable' WHERE IdempotencyKey = 'k3'");
        }

        SqliteConnection.ClearAllPools();
        await using (CatalogueDbContext migrating = Open(copy))
        {
            await new CatalogueMigrator(migrating).ApplyAsync();
        }

        SqliteConnection.ClearAllPools();
        await using (CatalogueDbContext migrated = Open(copy))
        {
            Assert.Empty(await migrated.Database.GetPendingMigrationsAsync());
            Assert.False(await migrated.Jobs.AnyAsync(job => job.JobType == "ExtractionFailed"));
            List<ExtractionIssueRow> issues = await migrated.ExtractionIssues.OrderBy(i => i.PageIndex).ToListAsync();
            Assert.Equal(2, issues.Count);
            Assert.Null(issues[0].PageIndex);
            Assert.Equal("search_book_extraction_failed", issues[0].Code);
            Assert.Equal(3, issues[1].PageIndex);
            Assert.Equal(49, issues[1].Occurrences);
            Assert.All(issues, issue => Assert.Equal(new string('a', 64), issue.ContentHash));

            JobRow embedding = await migrated.Jobs.SingleAsync(job => job.IdempotencyKey == "k3");
            Assert.Equal((int)JobRuntimeStatus.WaitingForCapability, embedding.Status);
            Assert.Equal(0, embedding.RetryCount);
            Assert.Equal(JobCapabilities.SemanticEmbeddings, embedding.WaitingCapability);

            JobRow thumbnail = await migrated.Jobs.SingleAsync(job => job.IdempotencyKey == "k4");
            Assert.Equal((int)JobRuntimeStatus.Completed, thumbnail.Status);
            Assert.Equal(1, thumbnail.RetryCount);
        }

        // Restore rehearsal: the verified backup still holds the previous state.
        string backup = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(copy)!, "catalogue.db.*.bak"));
        string restored = Path.Combine(_temp, "restored.db");
        File.Copy(backup, restored);
        await using (CatalogueDbContext restoredContext = Open(restored))
        {
            Assert.Equal(2, await restoredContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Jobs WHERE JobType = 'ExtractionFailed'")
                .SingleAsync());
            Assert.DoesNotContain(
                "20260925105740_Sept23Phase06JobAccountingAndIssues",
                await restoredContext.Database.GetAppliedMigrationsAsync());
        }
    }

    private static CatalogueDbContext Open(string path) =>
        new(new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlite($"Data Source={path};Pooling=False")
            .Options);
}
