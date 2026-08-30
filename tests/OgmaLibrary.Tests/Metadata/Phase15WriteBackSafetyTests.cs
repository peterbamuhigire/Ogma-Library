using System.Security.Cryptography;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Metadata;
using OgmaLibrary.Infrastructure.Sidecar;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Metadata;

/// <summary>Phase 15 acceptance tests for write-back source-change protection.</summary>
public sealed class Phase15WriteBackSafetyTests
{
    [Fact]
    public async Task WriteBackRejectsSourceChangedAfterBackupToken()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ogma-phase15-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "book.pdf");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);
        try
        {
            using CatalogueDbContext context = CatalogueTestHelper.CreateInMemoryContext();
            var service = new PdfWriteBackService(context, new SidecarService(root), root);
            string originalHash = Convert.ToHexStringLower(
                SHA256.HashData(await File.ReadAllBytesAsync(path)));
            var token = new BackupToken(
                Path.Combine(root, "backup.pdf"), path, originalHash);
            await File.WriteAllBytesAsync(path, [4, 5, 6]);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.WriteAsync(
                "01PH15BOOK0000000000000001",
                [new AcceptedFieldProposal("Title", "New title", "UserOverride", 1.0, true)],
                token));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
