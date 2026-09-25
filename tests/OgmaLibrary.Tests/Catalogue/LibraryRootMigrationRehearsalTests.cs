using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Infrastructure.Pdf;
using OgmaLibrary.Tests.Ingestion;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>
/// Sept-23 Phase 05 (T05.2) migration rehearsal: a copy of an existing single-root
/// catalogue (schema at the last pre-Phase-05 migration, 17 books) is migrated, the
/// pre-migration backup is verified and restored side by side, and every book stays
/// openable through its own library root after the per-root backfill.
/// </summary>
public sealed class LibraryRootMigrationRehearsalTests : IDisposable
{
    private const string PreviousMigration = "20260906060000_Phase17PausedJobStatus";
    private const int BookCount = 17;
    private readonly string _temp = Path.Combine(Path.GetTempPath(), $"ogma-p05-migration-{Guid.NewGuid():N}");

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
    public async Task LibraryRootMigration_OnCopyOfExistingCatalogue_KeepsAllBooksOpenable()
    {
        string library = Path.Combine(_temp, "library");
        string original = Path.Combine(_temp, "original", "catalogue.db");
        string copy = Path.Combine(_temp, "copy", "catalogue.db");
        Directory.CreateDirectory(Path.GetDirectoryName(original)!);
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);

        // 1. An existing install: previous schema, one legacy root, 17 books.
        await using (CatalogueDbContext legacy = Open(original))
        {
            await legacy.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            for (int index = 0; index < BookCount; index++)
            {
                string folder = index % 2 == 0 ? "Science" : "History";
                string relative = $"{folder}/book-{index:D2}.pdf";
                string absolute = Path.Combine(library, folder, $"book-{index:D2}.pdf");
                Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
                IngestionTestFixture.WriteSyntheticPdf(absolute, $"Book {index}", "Author");
                string bookId = $"01REHEARSALBOOK{index:D11}";
                await legacy.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO Books (BookId, Title, Status) VALUES ({bookId}, {"Book " + index}, 0)");
                await legacy.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO BookFiles (BookId, RelativePath, FileStatus, LastSeenUtc) VALUES ({bookId}, {relative}, 0, {DateTimeOffset.UtcNow})");
            }
        }

        SqliteConnection.ClearAllPools();
        File.Copy(original, copy);

        // 2. Migrate the copy with the production migrator (backup first).
        await using (CatalogueDbContext migrating = Open(copy))
        {
            await new CatalogueMigrator(migrating).ApplyAsync();
        }

        SqliteConnection.ClearAllPools();

        // 3. Restore rehearsal: the verified backup is the untouched previous schema.
        string backup = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(copy)!, "catalogue.db.*.bak"));
        string restored = Path.Combine(_temp, "restored.db");
        File.Copy(backup, restored);
        await using (CatalogueDbContext restoredContext = Open(restored))
        {
            Assert.Equal(BookCount, await restoredContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Books").SingleAsync());
            Assert.Equal(0, await restoredContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('BookFiles') WHERE name = 'LibraryRootId'")
                .SingleAsync());
            Assert.Contains(PreviousMigration, await restoredContext.Database.GetAppliedMigrationsAsync());
        }

        // 4. The migrated copy: new columns, transactional backfill, every book openable.
        using var settings = new LibrarySettingsService(Path.Combine(_temp, "data"));
        await settings.SetLibraryRootAsync(library);
        await using (CatalogueDbContext migrated = Open(copy))
        {
            Assert.Empty(await migrated.Database.GetPendingMigrationsAsync());
            string? rootId = await LibraryRootPaths.BackfillLegacyRootAsync(migrated, library, CancellationToken.None);
            Assert.NotNull(rootId);
            Assert.Equal(BookCount, await migrated.BookFiles.CountAsync(file => file.LibraryRootId == rootId));

            var locator = new BookFileLocator(migrated, settings);
            foreach (string bookId in await migrated.Books.Select(book => book.BookId).ToListAsync())
            {
                string? path = await locator.LocateAsync(bookId, CancellationToken.None);
                Assert.NotNull(path);
                Assert.True(File.Exists(path));
            }

            var readModel = new CatalogueReadModel(migrated);
            int visible = 0;
            await foreach (BookSummaryProjection _ in readModel.GetBookSummariesAsync(new CatalogueFilter()))
            {
                visible++;
            }

            Assert.Equal(BookCount, visible);
        }

        // 5. The original file is untouched (the recovery point of the rehearsal).
        await using (CatalogueDbContext untouched = Open(original))
        {
            Assert.Contains(PreviousMigration, await untouched.Database.GetAppliedMigrationsAsync());
            Assert.NotEmpty(await untouched.Database.GetPendingMigrationsAsync());
        }
    }

    private static CatalogueDbContext Open(string path) =>
        new(new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlite($"Data Source={path};Pooling=False")
            .Options);
}
