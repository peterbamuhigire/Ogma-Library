using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>Fails CI when the frozen beta migration sequence changes implicitly.</summary>
public sealed class Phase38ReleaseSchemaFreezeTests
{
    // Deliberately advanced by Sept-23 Phase 17: Books.TextStatus, TextQuality and OcrConfidence
    // (honest text status, K21) with a backup-and-restore rehearsal
    // (TextStatusMigrationRehearsalTests). Previous freeze: 43 migrations ending at
    // 20260925105740_Sept23Phase06JobAccountingAndIssues (ac22890a...c63ba3).
    // Earlier: deliberately advanced by Sept-23 Phase 06: job accounting columns (RequeueCount,
    // lease owner pid/start, WaitingCapability) and the ExtractionIssues table, with a
    // backup-and-restore rehearsal (JobAccountingMigrationRehearsalTests). Previous freeze:
    // 42 migrations ending at 20260925082519_Sept23Phase05LibraryRootsAndValidity (fb90055d...b4edb).
    // Earlier: deliberately advanced by Sept-23 Phase 05 (ADR-0018): per-root BookFiles,
    // FileValidity and FileIssues, with a verified backup and restore rehearsal
    // (LibraryRootMigrationRehearsalTests). Previous freeze: 41 migrations ending at
    // 20260906060000_Phase17PausedJobStatus (8135fad4...37dd5).
    private const int FrozenMigrationCount = 44;
    private const string FrozenLatestMigration = "20260925132534_Sept23Phase17TextStatus";
    private const string FrozenSequenceSha256 =
        "32588cbf39147ea0599a994cf1f04e4ac0f4fb674e1010a65e5fec130fc21d6f";

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
