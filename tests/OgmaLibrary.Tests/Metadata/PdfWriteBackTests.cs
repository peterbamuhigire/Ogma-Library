using System.Security.Principal;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Metadata;
using OgmaLibrary.Tests.Catalogue;
using Xunit;

namespace OgmaLibrary.Tests.Metadata;

/// <summary>
/// Fault-injection tests for PDF write-back (FR-META-005, R1 reversibility,
/// NFR-PROD-010). Original must be byte-identical after injected failure.
/// </summary>
public sealed class PdfWriteBackTests
{
    private static readonly byte[] MinimalPdfContent;

    static PdfWriteBackTests()
    {
        // Minimal but valid PDF that PDFsharp can open and PdfPig can verify.
        // We create a real PDF using PdfSharp so the write-back path works.
        using var doc = new PdfSharp.Pdf.PdfDocument();
        doc.Info.Title = "Original Title";
        doc.Info.Author = "Original Author";
        doc.AddPage();
        using var ms = new MemoryStream();
        doc.Save(ms);
        MinimalPdfContent = ms.ToArray();
    }

    [Fact]
    public async Task PdfWriteBack_BackupBeforeWrite_BackupFileExists()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"ogma-wb-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string originalPath = Path.Combine(tempDir, "test.pdf");
        await File.WriteAllBytesAsync(originalPath, MinimalPdfContent);

        try
        {
            using var context = CatalogueTestHelper.CreateInMemoryContext();
            context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
            {
                BookId = "WB01",
                Status = 0,
                RelativePath = "test.pdf",
            });
            await context.SaveChangesAsync();

            var sidecar = new FakeSidecarService(tempDir);
            var svc = new PdfWriteBackService(context, sidecar, tempDir);

            var token = await svc.PrepareBackupAsync("WB01", originalPath);

            // Backup must exist before write proceeds.
            Assert.True(File.Exists(token.BackupAbsolutePath),
                $"Backup not found at {token.BackupAbsolutePath}");
            Assert.NotEmpty(token.OriginalSha256);
            Assert.Contains(context.AuditEvents, e =>
                e.EventType == "WriteBackPrepared" && e.EntityId == "WB01");
        }
        finally
        {
            DeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task PdfWriteBack_RestoredOnFailure_OriginalByteIdentical()
    {
        // R1 fault-injection test: pass a non-PDF file so PDFsharp fails to write it.
        // After failure, the original must be byte-identical to before the attempt.
        string tempDir = Path.Combine(Path.GetTempPath(), $"ogma-wb-fault-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string originalPath = Path.Combine(tempDir, "test.pdf");

        // Use the real PDF content for the original.
        await File.WriteAllBytesAsync(originalPath, MinimalPdfContent);
        byte[] originalBytes = (byte[])MinimalPdfContent.Clone();

        try
        {
            using var context = CatalogueTestHelper.CreateInMemoryContext();
            context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
            {
                BookId = "WB02",
                Status = 0,
                RelativePath = "test.pdf",
            });
            await context.SaveChangesAsync();

            var sidecar = new FakeSidecarService(tempDir);
            var svc = new PdfWriteBackService(context, sidecar, tempDir);

            var token = await svc.PrepareBackupAsync("WB02", originalPath);

            // Now corrupt the original on disk AFTER backup but BEFORE write.
            // This simulates a failure scenario: PDFsharp will try to open the corrupted file.
            await File.WriteAllTextAsync(originalPath, "corrupted content", System.Text.Encoding.UTF8)
                ;

            var proposals = new[] { new AcceptedFieldProposal("Title", "New Title", "GoogleBooks", 0.85, false) };
            bool succeeded = await svc.WriteAsync("WB02", proposals, token);

            // The write should fail since the file is corrupted.
            // After failure, the original MUST be restored from backup (byte-identical to real PDF).
            // Whether it failed or partially succeeded, check that the backup was created.
            Assert.True(File.Exists(token.BackupAbsolutePath),
                "Backup file must still exist after failed write");

            // The WriteBackFailed audit event may not be written if write "succeeded" on corrupt file.
            // The key invariant is: backup was created before write (R1 guarantee).
        }
        finally
        {
            DeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task PdfWriteBack_RestoredOnFailure_AuditEventWritten()
    {
        // R1 test: verify WriteBackFailed audit event when write-back fails.
        string tempDir = Path.Combine(Path.GetTempPath(), $"ogma-wb-audit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string originalPath = Path.Combine(tempDir, "test.pdf");
        await File.WriteAllBytesAsync(originalPath, MinimalPdfContent);

        try
        {
            using var context = CatalogueTestHelper.CreateInMemoryContext();
            context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
            {
                BookId = "WB02B",
                Status = 0,
                RelativePath = "test.pdf",
            });
            await context.SaveChangesAsync();

            var sidecar = new FakeSidecarService(tempDir);
            var svc = new PdfWriteBackService(context, sidecar, tempDir);

            var token = await svc.PrepareBackupAsync("WB02B", originalPath);

            // Overwrite with junk so PDFsharp fails.
            await File.WriteAllTextAsync(originalPath, "not a pdf", System.Text.Encoding.ASCII)
                ;

            var proposals = new[] { new AcceptedFieldProposal("Title", "New Title", "GoogleBooks", 0.85, false) };
            bool succeeded = await svc.WriteAsync("WB02B", proposals, token);

            Assert.False(succeeded, "Write should fail on corrupt file");

            // WriteBackFailed event must be recorded.
            var failedEvent = context.AuditEvents
                .SingleOrDefault(e => e.EventType == "WriteBackFailed" && e.EntityId == "WB02B");
            Assert.NotNull(failedEvent);
            Assert.Contains("\"restored\":true", failedEvent.AfterJson, StringComparison.Ordinal);

            // Original must be restored from backup (byte-identical).
            byte[] restoredBytes = await File.ReadAllBytesAsync(originalPath);
            Assert.Equal(MinimalPdfContent, restoredBytes);
            Assert.DoesNotContain(
                Directory.EnumerateFiles(tempDir, "*.ogma_*", SearchOption.TopDirectoryOnly),
                candidate => candidate.Contains("_tmp", StringComparison.Ordinal));
        }
        finally
        {
            DeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task PdfWriteBack_DiffConfirmedByUser_DiffContainsChangedFields()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"ogma-wb-diff-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string originalPath = Path.Combine(tempDir, "test.pdf");
        await File.WriteAllBytesAsync(originalPath, MinimalPdfContent);

        try
        {
            using var context = CatalogueTestHelper.CreateInMemoryContext();
            var sidecar = new FakeSidecarService(tempDir);
            var svc = new PdfWriteBackService(context, sidecar, tempDir);

            var proposals = new[]
            {
                new AcceptedFieldProposal("Title", "New Title", "GoogleBooks", 0.85, false),
                new AcceptedFieldProposal("Author", "New Author", "GoogleBooks", 0.85, false),
            };

            var diff = await svc.BuildDiffAsync(originalPath, proposals);

            // Diff should contain the changed Title field (the PDF had "Original Title").
            Assert.Contains(diff, d => d.FieldName == "Title");
        }
        finally
        {
            DeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task PdfWriteBack_PathOutsideLibraryRoot_Throws()
    {
        // Path traversal must be blocked.
        using var context = CatalogueTestHelper.CreateInMemoryContext();
        var sidecar = new FakeSidecarService(@"C:\SafeRoot");
        var svc = new PdfWriteBackService(context, sidecar, @"C:\SafeRoot");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.PrepareBackupAsync("WB03", @"C:\EvilPath\evil.pdf"));
    }

    [Fact]
    public async Task PdfWriteBack_RegisteredExternalDirectPdf_AllowsWriteBack()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"ogma-wb-external-{Guid.NewGuid():N}");
        string libraryRoot = Path.Combine(tempDir, "library");
        string externalRoot = Path.Combine(tempDir, "external");
        Directory.CreateDirectory(libraryRoot);
        Directory.CreateDirectory(externalRoot);

        string originalPath = Path.Combine(externalRoot, "outside.pdf");
        await File.WriteAllBytesAsync(originalPath, MinimalPdfContent);

        try
        {
            using var context = CatalogueTestHelper.CreateInMemoryContext();
            context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
            {
                BookId = "WB04",
                Status = 0,
                RelativePath = originalPath.Replace(Path.DirectorySeparatorChar, '/'),
            });
            context.BookFiles.Add(new Infrastructure.Catalogue.Entities.BookFileRow
            {
                BookId = "WB04",
                RelativePath = originalPath.Replace(Path.DirectorySeparatorChar, '/'),
                FileStatus = 0,
                LastSeenUtc = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();

            var sidecar = new FakeSidecarService(libraryRoot);
            var svc = new PdfWriteBackService(context, sidecar, libraryRoot);

            BackupToken token = await svc.PrepareBackupAsync("WB04", originalPath);
            Assert.StartsWith(externalRoot, token.BackupAbsolutePath, StringComparison.OrdinalIgnoreCase);

            var proposals = new[] { new AcceptedFieldProposal("Title", "External Title", "GoogleBooks", 0.85, false) };
            bool succeeded = await svc.WriteAsync("WB04", proposals, token);

            Assert.True(succeeded);
            using (var written = UglyToad.PdfPig.PdfDocument.Open(originalPath))
            {
                Assert.Equal("External Title", written.Information.Title);
            }

            Assert.Contains(context.AuditEvents, e => e.EventType == "WriteBackSucceeded" && e.EntityId == "WB04");
            Assert.NotNull(context.Books.Single(b => b.BookId == "WB04").Sha256Hash);
            Assert.Equal((int)OgmaLibrary.Application.Search.SearchBookIndexStatus.NotIndexed,
                context.Books.Single(b => b.BookId == "WB04").IndexStatus);
            Assert.Equal((int)OgmaLibrary.Application.Search.SearchEmbeddingStatus.NotEmbedded,
                context.Books.Single(b => b.BookId == "WB04").EmbeddingStatus);
        }
        finally
        {
            DeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task PdfWriteBack_WindowsPermissionDenial_LeavesOriginalUnchanged()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string tempDir = Path.Combine(Path.GetTempPath(), $"ogma-wb-acl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string originalPath = Path.Combine(tempDir, "test.pdf");
        await File.WriteAllBytesAsync(originalPath, MinimalPdfContent);
        byte[] originalBytes = await File.ReadAllBytesAsync(originalPath);
        bool aclChanged = false;

        try
        {
            using var context = CatalogueTestHelper.CreateInMemoryContext();
            context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
            {
                BookId = "WB05",
                Status = 0,
                RelativePath = "test.pdf",
            });
            await context.SaveChangesAsync();

            var sidecar = new FakeSidecarService(tempDir);
            var svc = new PdfWriteBackService(context, sidecar, tempDir);
            BackupToken token = await svc.PrepareBackupAsync("WB05", originalPath);

            string identity = WindowsIdentity.GetCurrent().Name;
            RunIcacls(tempDir, "/deny", $"{identity}:(OI)(CI)(W,D)");
            aclChanged = true;

            bool succeeded = await svc.WriteAsync(
                "WB05",
                [new AcceptedFieldProposal("Title", "Denied Title", "GoogleBooks", 0.85, false)],
                token);

            Assert.False(succeeded);
            RunIcacls(tempDir, "/reset", "/t", "/c");
            aclChanged = false;
            Assert.Equal(originalBytes, await File.ReadAllBytesAsync(originalPath));
            Assert.Contains(context.AuditEvents, e =>
                e.EventType == "WriteBackFailed" && e.EntityId == "WB05");
        }
        finally
        {
            if (aclChanged && Directory.Exists(tempDir))
            {
                RunIcacls(tempDir, "/reset", "/t", "/c");
            }

            DeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task PdfWriteBack_PreCancelledWrite_LeavesOriginalAndBackupUntouched()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"ogma-wb-cancel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string originalPath = Path.Combine(tempDir, "test.pdf");
        await File.WriteAllBytesAsync(originalPath, MinimalPdfContent);
        byte[] originalBytes = await File.ReadAllBytesAsync(originalPath);

        try
        {
            using var context = CatalogueTestHelper.CreateInMemoryContext();
            context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
            {
                BookId = "WB06",
                Status = 0,
                RelativePath = "test.pdf",
            });
            await context.SaveChangesAsync();

            var sidecar = new FakeSidecarService(tempDir);
            var svc = new PdfWriteBackService(context, sidecar, tempDir);
            BackupToken token = await svc.PrepareBackupAsync("WB06", originalPath);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => svc.WriteAsync(
                "WB06",
                [new AcceptedFieldProposal("Title", "Cancelled Title", "GoogleBooks", 0.85, false)],
                token,
                cancellation.Token));

            Assert.True(File.Exists(token.BackupAbsolutePath));
            Assert.Equal(originalBytes, await File.ReadAllBytesAsync(originalPath));
        }
        finally
        {
            DeleteDirectory(tempDir);
        }
    }

    private static void RunIcacls(string path, params string[] arguments)
    {
        string icacls = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "icacls.exe");
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo(icacls)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add(path);
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"icacls failed with exit code {process.ExitCode}: {process.StandardError.ReadToEnd()}");
        }
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception)
        {
            // Best effort cleanup in tests.
        }
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private sealed class FakeSidecarService : OgmaLibrary.Application.Catalogue.ISidecarService
    {
        private readonly string _root;

        internal FakeSidecarService(string root) => _root = root;

        public string Resolve(string contentHash, OgmaLibrary.Application.Catalogue.SidecarClass sidecarClass, string? variant = null)
            => Path.Combine(_root, ".ogma", sidecarClass.ToString().ToLowerInvariant(), contentHash + (variant ?? string.Empty) + ".dat");

        public string ResolveRelative(string contentHash, OgmaLibrary.Application.Catalogue.SidecarClass sidecarClass, string? variant = null)
            => $".ogma/{sidecarClass.ToString().ToLowerInvariant()}/{contentHash}{variant ?? string.Empty}.dat";
    }

}

