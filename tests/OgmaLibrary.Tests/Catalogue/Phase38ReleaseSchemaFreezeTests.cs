using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>Fails CI when the frozen beta migration sequence changes implicitly.</summary>
public sealed class Phase38ReleaseSchemaFreezeTests
{
    // Deliberately advanced by Sept-23 Phase 05 (ADR-0018): per-root BookFiles,
    // FileValidity and FileIssues, with a verified backup and restore rehearsal
    // (LibraryRootMigrationRehearsalTests). Previous freeze: 41 migrations ending at
    // 20260906060000_Phase17PausedJobStatus (8135fad4...37dd5).
    private const int FrozenMigrationCount = 42;
    private const string FrozenLatestMigration = "20260925082519_Sept23Phase05LibraryRootsAndValidity";
    private const string FrozenSequenceSha256 =
        "fb90055d64a0e039800d5f2d7782e93d2b28064267dbe08fa1bd11e3604b4edb";

    [Fact]
    public void ReleaseSchema_BetaV1MigrationSequence_IsFrozen()
    {
        (Infrastructure.Catalogue.CatalogueDbContext context, string dbPath) =
            CatalogueTestHelper.CreateTempFileContext();
        try
        {
            using (context)
            {
                string[] migrations = context.Database.GetMigrations().ToArray();
                string sequence = string.Join('\n', migrations);
                string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sequence)));

                Assert.Equal(FrozenMigrationCount, migrations.Length);
                Assert.Equal(FrozenLatestMigration, migrations[^1]);
                Assert.Equal(FrozenSequenceSha256, hash);
            }
        }
        finally
        {
            CatalogueTestHelper.DeleteTempDb(dbPath);
        }
    }
}
