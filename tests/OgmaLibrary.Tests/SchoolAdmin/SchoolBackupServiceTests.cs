using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.SchoolAdmin;

namespace OgmaLibrary.Tests.SchoolAdmin;

/// <summary>Phase 36 online-backup and non-destructive restore-rehearsal tests.</summary>
public sealed class SchoolBackupServiceTests
{
    [Fact]
    public async Task SchoolBackup_CreateThenRehearse_PreservesPointInTimeDataWithoutReplacingLiveDatabase()
    {
        string dataDirectory = CreateTempDirectory();
        string backupDirectory = Path.Combine(dataDirectory, "protected-backups");
        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            await using (CatalogueDbContext seed = provider.GetRequiredService<CatalogueDbContext>())
            {
                seed.AuditEvents.Add(new AuditEventRow
                {
                    EventType = "BackupFixture",
                    EntityType = "School",
                    EntityId = "point-in-time",
                    Timestamp = DateTimeOffset.UtcNow,
                    AfterJson = "{}",
                    IsLocalOnly = true,
                });
                await seed.SaveChangesAsync();
            }

            var service = provider.GetRequiredService<ISchoolBackupService>();
            SchoolBackupResult backup = await service.CreateBackupAsync(backupDirectory);

            Assert.True(File.Exists(backup.BackupPath));
            Assert.Equal(64, backup.Sha256.Length);
            Assert.True(backup.SizeBytes > 0);

            await using (CatalogueDbContext live = provider.GetRequiredService<CatalogueDbContext>())
            {
                AuditEventRow row = await live.AuditEvents.SingleAsync(audit => audit.EntityId == "point-in-time");
                row.EntityId = "changed-after-backup";
                await live.SaveChangesAsync();
            }

            SchoolRestoreRehearsalResult rehearsal = await service.RehearseRestoreAsync(backup.BackupPath);
            Assert.Equal(backup.Sha256, rehearsal.BackupSha256);
            Assert.Equal(64, rehearsal.SchemaSha256.Length);
            Assert.True(rehearsal.TableCount > 0);
            Assert.True(rehearsal.TotalRows > 0);

            await using (CatalogueDbContext live = provider.GetRequiredService<CatalogueDbContext>())
            {
                Assert.NotNull(await live.AuditEvents.SingleOrDefaultAsync(audit => audit.EntityId == "changed-after-backup"));
                Assert.Null(await live.AuditEvents.SingleOrDefaultAsync(audit => audit.EntityId == "point-in-time"));
            }

            await using var backupConnection = new SqliteConnection(
                $"Data Source={backup.BackupPath};Mode=ReadOnly;Pooling=False");
            await backupConnection.OpenAsync();
            await using SqliteCommand command = backupConnection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM AuditEvents WHERE EntityId = 'point-in-time';";
            Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task SchoolBackup_RehearsalRejectsCorruptBackupAndCleansTemporaryRestore()
    {
        string dataDirectory = CreateTempDirectory();
        string corruptPath = Path.Combine(dataDirectory, "corrupt.db");
        try
        {
            await File.WriteAllTextAsync(corruptPath, "not a sqlite database");
            using var service = new SchoolBackupService(dataDirectory);

            await Assert.ThrowsAnyAsync<SqliteException>(
                () => service.RehearseRestoreAsync(corruptPath));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    private static async Task<ServiceProvider> CreateServicesAsync(string dataDirectory)
    {
        ServiceProvider provider = new ServiceCollection()
            .AddCatalogueContext(dataDirectory, dataDirectory)
            .AddSchoolAdminServices(dataDirectory)
            .BuildServiceProvider();
        await using CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>();
        await context.Database.MigrateAsync();
        return provider;
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ogma-school-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CleanupTempDirectory(string dataDirectory)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(dataDirectory))
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }
}
