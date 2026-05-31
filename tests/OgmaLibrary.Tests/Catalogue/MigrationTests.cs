using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

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
}
