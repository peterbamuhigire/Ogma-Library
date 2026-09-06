using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>Fails CI when the frozen beta migration sequence changes implicitly.</summary>
public sealed class Phase38ReleaseSchemaFreezeTests
{
    private const int FrozenMigrationCount = 41;
    private const string FrozenLatestMigration = "20260906060000_Phase17PausedJobStatus";
    private const string FrozenSequenceSha256 =
        "8135fad43778f705b48c9d667d8e56d36b8d4445b8be3a5d2b985b1e42637dd5";

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
