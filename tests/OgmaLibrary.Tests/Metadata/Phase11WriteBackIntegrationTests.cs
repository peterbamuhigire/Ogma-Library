using System.Security.Cryptography;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Metadata;
using OgmaLibrary.Infrastructure.Sidecar;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Metadata;

/// <summary>
/// Sept-23 Phase 11 slice 5 (task 11.6): the inspector's two-step write-back drives the real
/// <see cref="PdfWriteBackService"/>. Writing to a fixture copy and restoring returns the original
/// SHA-256; a read-only file is never modified.
/// </summary>
public sealed class Phase11WriteBackIntegrationTests
{
    private const string BookId = "PHASE11WRITEBACK0000000001";

    [Fact]
    public async Task Phase11WriteBack_WriteThenRestore_ReturnsOriginalSha256()
    {
        string root = CreateRoot();
        string path = Path.Combine(root, "book.pdf");
        await File.WriteAllBytesAsync(path, CreatePdf("Original title"));
        string originalHash = await HashAsync(path);
        try
        {
            using CatalogueDbContext context = await CreateContextAsync();
            var service = new PdfWriteBackService(context, new SidecarService(root), root);

            BackupToken token = await service.PrepareBackupAsync(BookId, path);
            bool written = await service.WriteAsync(BookId, [Title("Changed title")], token);
            string writtenHash = await HashAsync(path);
            bool restored = await service.RestoreBackupAsync(BookId, token);

            Assert.True(written);
            Assert.NotEqual(originalHash, writtenHash);
            Assert.True(restored);
            Assert.Equal(originalHash, await HashAsync(path));
            Assert.Equal(originalHash, token.OriginalSha256);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Phase11WriteBack_ReadOnlyFile_FailsAndLeavesFileUntouched()
    {
        string root = CreateRoot();
        string path = Path.Combine(root, "book.pdf");
        await File.WriteAllBytesAsync(path, CreatePdf("Original title"));
        string originalHash = await HashAsync(path);
        try
        {
            using CatalogueDbContext context = await CreateContextAsync();
            var service = new PdfWriteBackService(context, new SidecarService(root), root);
            BackupToken token = await service.PrepareBackupAsync(BookId, path);
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);

            bool written = await service.WriteAsync(BookId, [Title("Changed title")], token);

            Assert.False(written);
            Assert.Equal(originalHash, await HashAsync(path));
            Assert.True(File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }

            DeleteRoot(root);
        }
    }

    private static AcceptedFieldProposal Title(string value) =>
        new("Title", value, "UserOverride", 1.0, true);

    private static async Task<CatalogueDbContext> CreateContextAsync()
    {
        CatalogueDbContext context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
        {
            BookId = BookId,
            Status = 0,
            RelativePath = "book.pdf",
        });
        await context.SaveChangesAsync();
        return context;
    }

    private static byte[] CreatePdf(string title)
    {
        using var document = new PdfSharp.Pdf.PdfDocument();
        document.Info.Title = title;
        document.AddPage();
        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    private static async Task<string> HashAsync(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(path)));

    private static string CreateRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ogma-phase11-wb-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRoot(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(root, recursive: true);
    }
}
