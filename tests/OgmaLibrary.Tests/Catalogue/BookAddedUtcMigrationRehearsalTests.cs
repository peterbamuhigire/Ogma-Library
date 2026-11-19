using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>
/// Sept-23 Phase 10 migration rehearsal: a copy of a catalogue at the Phase 17 schema is migrated
/// with the production migrator. Existing books get no invented date added (it stays unknown and
/// sorts last), and the verified backup restores the previous schema.
/// </summary>
public sealed class BookAddedUtcMigrationRehearsalTests : IDisposable
{
    private const string PreviousMigration = "20260925132534_Sept23Phase17TextStatus";
    private const string NewMigration = "20261119102849_Sept23Phase10BookAddedUtc";
    private readonly string _temp = Path.Combine(Path.GetTempPath(), $"ogma-p10-migration-{Guid.NewGuid():N}");

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
    public async Task AddedUtcMigration_KeepsExistingBooksUnknown_AndBackupRestores()
    {
        string copy = Path.Combine(_temp, "copy", "catalogue.db");
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        await using (CatalogueDbContext legacy = Open(copy))
        {
            await legacy.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            string hash = new('d', 64);
            await legacy.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO Books (BookId, Title, Status, Sha256Hash) VALUES ('OLD-A', 'Older Atlas', 0, {hash}), ('OLD-B', 'Older Botany', 0, {hash})");
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
            Assert.All(await migrated.Books.AsNoTracking().ToListAsync(), book => Assert.Null(book.AddedUtc));

            var titles = new List<string?>();
            await foreach (BookSummaryProjection book in new CatalogueReadModel(migrated).GetBookSummariesAsync(
                               new CatalogueFilter(SortField: CatalogueSortField.DateAdded, SortAscending: false)))
            {
                Assert.Null(book.DateAddedUtc);
                titles.Add(book.Title);
            }

            Assert.Equal(["Older Atlas", "Older Botany"], titles);
        }

        // Restore rehearsal: the verified backup still holds the previous schema.
        string backup = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(copy)!, "catalogue.db.*.bak"));
        string restored = Path.Combine(_temp, "restored.db");
        File.Copy(backup, restored);
        await using (CatalogueDbContext restoredContext = Open(restored))
        {
            Assert.DoesNotContain(NewMigration, await restoredContext.Database.GetAppliedMigrationsAsync());
            Assert.Equal(0, await restoredContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('Books') WHERE name = 'AddedUtc'")
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
