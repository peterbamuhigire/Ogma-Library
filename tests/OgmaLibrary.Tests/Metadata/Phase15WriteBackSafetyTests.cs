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

    [Fact]
    public async Task WriteBackUndo_RestoresPreparedBytesAndRetainsBackup()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ogma-phase15-undo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "book.pdf");
        byte[] originalBytes;
        using (var document = new PdfSharp.Pdf.PdfDocument())
        {
            document.Info.Title = "Original title";
            document.AddPage();
            using var stream = new MemoryStream();
            document.Save(stream);
            originalBytes = stream.ToArray();
        }
        await File.WriteAllBytesAsync(path, originalBytes);

        try
        {
            using CatalogueDbContext context = CatalogueTestHelper.CreateInMemoryContext();
            context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
            {
                BookId = "PHASE15UNDO000000000000001",
                Status = 0,
                RelativePath = "book.pdf",
            });
            await context.SaveChangesAsync();
            var service = new PdfWriteBackService(context, new SidecarService(root), root);

            BackupToken token = await service.PrepareBackupAsync(
                "PHASE15UNDO000000000000001",
                path);
            bool written = await service.WriteAsync(
                "PHASE15UNDO000000000000001",
                [new AcceptedFieldProposal("Title", "Changed title", "UserOverride", 1.0, true)],
                token);
            bool undone = await service.RestoreBackupAsync(
                "PHASE15UNDO000000000000001",
                token);

            Assert.True(written);
            Assert.True(undone);
            Assert.Equal(originalBytes, await File.ReadAllBytesAsync(path));
            Assert.True(File.Exists(token.BackupAbsolutePath));
            Assert.Contains(context.AuditEvents, audit =>
                audit.EventType == "WriteBackUndone" && audit.EntityId == "PHASE15UNDO000000000000001");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WriteBackPlan_SurvivesServiceRecreation()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ogma-phase15-plan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "book.pdf");
        using (var document = new PdfSharp.Pdf.PdfDocument())
        {
            document.AddPage();
            using var stream = new MemoryStream();
            document.Save(stream);
            await File.WriteAllBytesAsync(path, stream.ToArray());
        }

        try
        {
            using CatalogueDbContext firstContext = CatalogueTestHelper.CreateInMemoryContext();
            firstContext.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
            {
                BookId = "PHASE15PLAN000000000000001",
                Status = 0,
                RelativePath = "book.pdf",
            });
            await firstContext.SaveChangesAsync();
            var firstService = new PdfWriteBackService(firstContext, new SidecarService(root), root);

            BackupToken prepared = await firstService.PrepareBackupAsync(
                "PHASE15PLAN000000000000001",
                path);

            using CatalogueDbContext restartedContext = CatalogueTestHelper.CreateInMemoryContext();
            var restartedService = new PdfWriteBackService(restartedContext, new SidecarService(root), root);
            WriteBackPlan? plan = await restartedService.GetWriteBackPlanAsync(
                "PHASE15PLAN000000000000001");

            Assert.NotNull(plan);
            Assert.Equal("prepared", plan.Status);
            Assert.Equal(prepared.OriginalSha256, plan.BackupToken.OriginalSha256);
            Assert.Equal(prepared.BackupAbsolutePath, plan.BackupToken.BackupAbsolutePath);
            Assert.True(File.Exists(plan.BackupToken.BackupAbsolutePath));
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
