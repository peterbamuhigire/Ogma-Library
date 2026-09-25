using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Ocr;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>
/// Sept-23 Phase 17 migration rehearsal: a copy of a catalogue at the Phase 06 schema, holding
/// an image-only book and a text book with extracted pages (the audit's K21 shape, both
/// "Indexed"), is migrated with the production migrator. The new text status starts Unknown,
/// the first status sweep assesses both books honestly, and the verified backup restores the
/// previous schema.
/// </summary>
public sealed class TextStatusMigrationRehearsalTests : IDisposable
{
    private const string PreviousMigration = "20260925105740_Sept23Phase06JobAccountingAndIssues";
    private const string NewMigration = "20260925132534_Sept23Phase17TextStatus";
    private readonly string _temp = Path.Combine(Path.GetTempPath(), $"ogma-p17-migration-{Guid.NewGuid():N}");

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
    public async Task TextStatusMigration_AddsColumns_AndFirstSweepAssessesExistingBooks()
    {
        string copy = Path.Combine(_temp, "copy", "catalogue.db");
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        await using (CatalogueDbContext legacy = Open(copy))
        {
            await legacy.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            string hash = new('c', 64);
            await legacy.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO Books (BookId, Title, Status, Sha256Hash, IndexStatus) VALUES ('SCAN', 'Scanned Pamphlet', 0, {hash}, 2), ('TEXT', 'Text Book', 0, {hash}, 2)");
            await legacy.Database.ExecuteSqlRawAsync(
                "INSERT INTO ExtractedPages (BookId, PageNumber, TextContent, ExtractionQuality, WordCount, Source, ExtractorVersion, IsSelectedText) VALUES " +
                "('SCAN', 0, NULL, 3, 0, 'Extraction', 'pdf-text-v1', 1), " +
                "('TEXT', 0, 'plain searchable words on the first page of the text book', 0, 11, 'Extraction', 'pdf-text-v1', 1)");
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
            Assert.All(await migrated.Books.AsNoTracking().ToListAsync(), book =>
            {
                Assert.Equal((int)BookTextStatus.Unknown, book.TextStatus);
                Assert.Equal(0, book.TextQuality);
                Assert.Null(book.OcrConfidence);
            });

            Assert.Equal(2, await new BookTextStatusService(migrated).RefreshPendingAsync(50));
            migrated.ChangeTracker.Clear();
            Assert.Equal((int)BookTextStatus.ImageOnly, (await migrated.Books.SingleAsync(book => book.BookId == "SCAN")).TextStatus);
            Assert.Equal((int)BookTextStatus.Searchable, (await migrated.Books.SingleAsync(book => book.BookId == "TEXT")).TextStatus);
        }

        // Restore rehearsal: the verified backup still holds the previous schema.
        string backup = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(copy)!, "catalogue.db.*.bak"));
        string restored = Path.Combine(_temp, "restored.db");
        File.Copy(backup, restored);
        await using (CatalogueDbContext restoredContext = Open(restored))
        {
            Assert.DoesNotContain(NewMigration, await restoredContext.Database.GetAppliedMigrationsAsync());
            Assert.Equal(0, await restoredContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('Books') WHERE name = 'TextStatus'")
                .SingleAsync());
            Assert.Equal(2, await restoredContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Books")
                .SingleAsync());
        }
    }

    private static CatalogueDbContext Open(string path) =>
        new(new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlite($"Data Source={path};Pooling=False")
            .Options);
}
