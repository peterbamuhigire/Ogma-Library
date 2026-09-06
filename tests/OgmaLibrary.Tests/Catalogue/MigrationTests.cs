using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Ingestion;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>
/// Integration tests for the migration service and migration backup/restore path
/// (NFR-PROD-010, NFR-PROD-012, Phase 04 deliverable 10).
/// </summary>
public sealed class MigrationTests
{
    [Fact]
    public async Task Migration_BackupBeforeApply_RestoredOnFailure()
    {
        // Arrange — create a temp SQLite DB and apply migrations.
        string dbPath = Path.Combine(Path.GetTempPath(), $"ogma-migtest-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<CatalogueDbContext>()
                .UseSqlite($"Data Source={dbPath};Pooling=False")
                .Options;

            // Apply migrations and insert a sentinel row.
            await using (var ctx = new CatalogueDbContext(options))
            {
                await ctx.Database.MigrateAsync();
                ctx.AuditEvents.Add(new AuditEventRow
                {
                    EventType = "SentinelEvent",
                    Timestamp = DateTimeOffset.UtcNow,
                    IsLocalOnly = true,
                });
                await ctx.SaveChangesAsync();
            }

            // Close all connections before reading the file bytes.
            SqliteConnection.ClearAllPools();
            byte[] originalBytes = await File.ReadAllBytesAsync(dbPath);

            // Act — run the migrator again (no pending migrations).
            await using (var ctx2 = new CatalogueDbContext(options))
            {
                var migrator = new CatalogueMigrator(ctx2);
                await migrator.ApplyAsync(); // idempotent, no backup
            }

            // Close and re-read.
            SqliteConnection.ClearAllPools();
            byte[] finalBytes = await File.ReadAllBytesAsync(dbPath);

            // Assert — no changes since no pending migrations.
            Assert.Equal(originalBytes.Length, finalBytes.Length);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            CatalogueTestHelper.DeleteTempDb(dbPath);
            foreach (string bak in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(dbPath) + "*.bak"))
            {
                File.Delete(bak);
            }
        }
    }

    [Fact]
    public async Task Migration_BackupFile_CreatedBeforeApply()
    {
        // Verify the backup path: when a DB file exists and migrations are pending,
        // CatalogueMigrator creates a timestamped .bak before applying.
        //
        // We create a real (empty) SQLite DB using a raw SqliteConnection,
        // so the __EFMigrationsHistory table does not exist — all migrations are pending.
        string dbPath = Path.Combine(Path.GetTempPath(), $"ogma-backupnew-{Guid.NewGuid():N}.db");

        try
        {
            // Create a real SQLite file so there IS a pre-existing DB to back up.
            using (var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False"))
            {
                await conn.OpenAsync();
                // No tables yet — __EFMigrationsHistory doesn't exist.
            }

            SqliteConnection.ClearAllPools();
            Assert.True(File.Exists(dbPath), "Setup: DB file should exist before migrator runs.");

            var options = new DbContextOptionsBuilder<CatalogueDbContext>()
                .UseSqlite($"Data Source={dbPath};Pooling=False")
                .Options;

            // Act — apply migrations on the empty (but real) SQLite DB.
            await using var ctx = new CatalogueDbContext(options);
            var migrator = new CatalogueMigrator(ctx);
            await migrator.ApplyAsync();

            SqliteConnection.ClearAllPools();

            // Assert — at least one .bak file was created before the migration ran.
            string[] bakFiles = Directory.GetFiles(
                Path.GetTempPath(),
                Path.GetFileName(dbPath) + "*.bak");

            Assert.True(bakFiles.Length > 0,
                "Expected at least one .bak file to be created before the migration applied to a pre-existing DB.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            CatalogueTestHelper.DeleteTempDb(dbPath);
            foreach (string bak in Directory.GetFiles(
                Path.GetTempPath(),
                Path.GetFileName(dbPath) + "*.bak"))
            {
                File.Delete(bak);
            }
        }
    }

    [Fact]
    public async Task Migration_IsIdempotent_WhenRunTwice()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"ogma-idem-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<CatalogueDbContext>()
                .UseSqlite($"Data Source={dbPath};Pooling=False")
                .Options;

            // First apply.
            await using (var ctx1 = new CatalogueDbContext(options))
            {
                var migrator1 = new CatalogueMigrator(ctx1);
                await migrator1.ApplyAsync();
            }

            SqliteConnection.ClearAllPools();

            // Second apply — must not throw.
            await using (var ctx2 = new CatalogueDbContext(options))
            {
                var migrator2 = new CatalogueMigrator(ctx2);
                var ex = await Record.ExceptionAsync(() => migrator2.ApplyAsync());
                Assert.Null(ex);
            }

            // DB must still be readable after both applies.
            await using (var ctx3 = new CatalogueDbContext(options))
            {
                Assert.Equal(0, await ctx3.Books.CountAsync());
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            CatalogueTestHelper.DeleteTempDb(dbPath);
            foreach (string bak in Directory.GetFiles(
                Path.GetTempPath(),
                Path.GetFileName(dbPath) + "*.bak"))
            {
                File.Delete(bak);
            }
        }
    }

    [Fact]
    public async Task Migration_RepairsMissingModelTable_WhenHistorySaysCurrent()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"ogma-repair-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<CatalogueDbContext>()
                .UseSqlite($"Data Source={dbPath};Pooling=False")
                .Options;

            await using (var setup = new CatalogueDbContext(options))
            {
                await setup.Database.MigrateAsync();
                await setup.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF;");
                await setup.Database.ExecuteSqlRawAsync("DROP TABLE BookFiles;");
                await setup.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");
            }

            SqliteConnection.ClearAllPools();

            await using (var repaired = new CatalogueDbContext(options))
            {
                var migrator = new CatalogueMigrator(repaired);
                await migrator.ApplyAsync();

                string bookId = await new BookRegistrationService(repaired).RegisterAsync(
                    new DiscoveredFile("repair.pdf", "repair.pdf", 10, 1234),
                    new string('a', 64));

                Assert.Equal(1, await repaired.BookFiles.CountAsync(f => f.BookId == bookId));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            CatalogueTestHelper.DeleteTempDb(dbPath);
            foreach (string bak in Directory.GetFiles(
                Path.GetTempPath(),
                Path.GetFileName(dbPath) + "*.bak"))
            {
                File.Delete(bak);
            }
        }
    }

    [Fact]
    public async Task Migration_RepairsPrecreatedPhase18Tables_WhenHistoryRowMissing()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"ogma-p18-history-repair-{Guid.NewGuid():N}.db");
        const string phase18Migration = "20260602072445_Phase18SchoolAdminTables";

        try
        {
            var options = new DbContextOptionsBuilder<CatalogueDbContext>()
                .UseSqlite($"Data Source={dbPath};Pooling=False")
                .Options;

            await using (var setup = new CatalogueDbContext(options))
            {
                await setup.Database.MigrateAsync();
                await setup.Database.ExecuteSqlRawAsync(
                    "DELETE FROM __EFMigrationsHistory WHERE MigrationId = {0};",
                    phase18Migration);
                Assert.Equal(0, await setup.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM __EFMigrationsHistory WHERE MigrationId = '20260602072445_Phase18SchoolAdminTables'")
                    .SingleAsync());
                Assert.Equal(1, await setup.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'AiUsageLedger'")
                    .SingleAsync());
            }

            SqliteConnection.ClearAllPools();

            await using (var repaired = new CatalogueDbContext(options))
            {
                var migrator = new CatalogueMigrator(repaired);
                await migrator.ApplyAsync();

                Assert.Equal(1, await repaired.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM __EFMigrationsHistory WHERE MigrationId = '20260602072445_Phase18SchoolAdminTables'")
                    .SingleAsync());
                Assert.Equal(0, await repaired.Books.CountAsync());
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            CatalogueTestHelper.DeleteTempDb(dbPath);
            foreach (string bak in Directory.GetFiles(
                Path.GetTempPath(),
                Path.GetFileName(dbPath) + "*.bak"))
            {
                File.Delete(bak);
            }
        }
    }

    [Fact]
    public async Task Phase16Migration_AddsLanHostTablesAndRoundTrips()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"ogma-p16-schema-{Guid.NewGuid():N}.db");
        string[] expectedTables = ["HostClientSessions", "HostModeSettings"];
        string[] expectedIndexes =
        [
            "IX_HostClientSessions_ClientId_ExpiresUtc",
            "IX_HostClientSessions_RevokedUtc",
        ];

        try
        {
            var options = new DbContextOptionsBuilder<CatalogueDbContext>()
                .UseSqlite($"Data Source={dbPath};Pooling=False")
                .Options;

            await using (var phase16 = new CatalogueDbContext(options))
            {
                IMigrator migrator = phase16.Database.GetService<IMigrator>();
                await migrator.MigrateAsync("20260601184330_Phase16LanHostTables");
                Assert.Equal(
                    expectedTables.OrderBy(name => name, StringComparer.Ordinal),
                    (await ReadObjectNamesAsync(phase16, "table", expectedTables))
                        .OrderBy(name => name, StringComparer.Ordinal));
                Assert.Equal(
                    expectedIndexes.OrderBy(name => name, StringComparer.Ordinal),
                    (await ReadObjectNamesAsync(phase16, "index", expectedIndexes))
                        .OrderBy(name => name, StringComparer.Ordinal));
                Assert.Equal(1, await phase16.HostModeSettings.CountAsync());
                Assert.False((await phase16.HostModeSettings.SingleAsync()).IsEnabled);
            }

            SqliteConnection.ClearAllPools();

            await using (var downgraded = new CatalogueDbContext(options))
            {
                IMigrator migrator = downgraded.Database.GetService<IMigrator>();
                await migrator.MigrateAsync("20260601171443_Phase15SmartShelfIndexes");
                Assert.Empty(await ReadObjectNamesAsync(downgraded, "table", expectedTables));
            }

            SqliteConnection.ClearAllPools();

            await using (var remigrated = new CatalogueDbContext(options))
            {
                IMigrator migrator = remigrated.Database.GetService<IMigrator>();
                await migrator.MigrateAsync("20260601184330_Phase16LanHostTables");
                Assert.Equal(
                    expectedTables.OrderBy(name => name, StringComparer.Ordinal),
                    (await ReadObjectNamesAsync(remigrated, "table", expectedTables))
                        .OrderBy(name => name, StringComparer.Ordinal));
                Assert.Equal(1, await remigrated.HostModeSettings.CountAsync());
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            CatalogueTestHelper.DeleteTempDb(dbPath);
        }
    }

    [Fact]
    public async Task Phase18Migration_AddsSchoolAdminTablesAndRoundTrips()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"ogma-p18-schema-{Guid.NewGuid():N}.db");
        string[] expectedTables =
        [
            "LibraryPublishSettings",
            "SharedShelves",
            "SharedShelfBooks",
            "EnrolledProfiles",
            "SchoolAiEntitlements",
            "AiUsageLedger",
        ];
        string[] expectedIndexes =
        [
            "IX_LibraryPublishSettings_IsPublished",
            "UX_LibraryPublishSettings_SourcePath",
            "IX_SharedShelves_Visibility_IsDeleted",
            "IX_SharedShelfBooks_BookId",
            "UX_EnrolledProfiles_EnrollmentToken",
            "IX_EnrolledProfiles_Role_RevokedUtc",
            "UX_AiUsageLedger_ProfileId_Date",
            "IX_AiUsageLedger_Date",
        ];

        try
        {
            var options = new DbContextOptionsBuilder<CatalogueDbContext>()
                .UseSqlite($"Data Source={dbPath};Pooling=False")
                .Options;

            await using (var migrated = new CatalogueDbContext(options))
            {
                await migrated.Database.MigrateAsync();
                IReadOnlyList<string> tables = await ReadObjectNamesAsync(migrated, "table", expectedTables);
                IReadOnlyList<string> indexes = await ReadObjectNamesAsync(migrated, "index", expectedIndexes);

                Assert.Equal(
                    expectedTables.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                    tables.OrderBy(name => name, StringComparer.Ordinal).ToArray());
                Assert.Equal(
                    expectedIndexes.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                    indexes.OrderBy(name => name, StringComparer.Ordinal).ToArray());
            }

            SqliteConnection.ClearAllPools();

            await using (var downgraded = new CatalogueDbContext(options))
            {
                IMigrator migrator = downgraded.Database.GetService<IMigrator>();
                await migrator.MigrateAsync("20260601184330_Phase16LanHostTables");
                Assert.Empty(await ReadObjectNamesAsync(downgraded, "table", expectedTables));
            }

            SqliteConnection.ClearAllPools();

            await using (var remigrated = new CatalogueDbContext(options))
            {
                await remigrated.Database.MigrateAsync();
                IReadOnlyList<string> tables = await ReadObjectNamesAsync(remigrated, "table", expectedTables);

                Assert.Equal(
                    expectedTables.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                    tables.OrderBy(name => name, StringComparer.Ordinal).ToArray());
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            CatalogueTestHelper.DeleteTempDb(dbPath);
        }
    }

    private static async Task<IReadOnlyList<string>> ReadObjectNamesAsync(
        CatalogueDbContext context,
        string objectType,
        IReadOnlyList<string> names)
    {
        List<string> found = await context.Database
            .SqlQueryRaw<string>(
                "SELECT name AS Value FROM sqlite_master WHERE type = {0}",
                objectType)
            .ToListAsync();
        return found
            .Where(names.Contains)
            .ToList();
    }
}
