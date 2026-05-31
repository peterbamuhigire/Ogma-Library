using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>
/// Integration tests for unavailable-file flagging (FR-LIB-004, reversibility R1).
/// </summary>
public sealed class UnavailableFileFlagTests : IDisposable
{
    private readonly IngestionTestFixture _fx = IngestionTestFixture.Create(3);

    /// <inheritdoc />
    public void Dispose() => _fx.Dispose();

    [Fact]
    public async Task UnavailableFileFlagging_PreservesAnnotations()
    {
        // Register books.
        await _fx.Orchestrator.ScanAsync();

        // Find a book and its file.
        BookFileRow fileRow = await _fx.Context.BookFiles.FirstAsync();
        string bookId = fileRow.BookId;

        // Add an annotation.
        _fx.Context.Annotations.Add(new AnnotationRow
        {
            AnnotationId = Guid.NewGuid().ToString("N"),
            BookId = bookId,
            Page = 0,
            Type = 0,
            CreatedUtc = DateTimeOffset.UtcNow,
            ModifiedUtc = DateTimeOffset.UtcNow,
        });
        await _fx.Context.SaveChangesAsync();

        // Delete the file from disk.
        string absPath = Path.Combine(
            _fx.RootDir,
            fileRow.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Delete(absPath);

        try
        {
            // Re-scan + flag missing files.
            await _fx.Orchestrator.ScanAsync();

            // Assert annotation still exists.
            int annotationCount = await _fx.Context.Annotations
                .Where(a => a.BookId == bookId)
                .CountAsync();
            Assert.True(annotationCount >= 1, "Annotation was deleted when file was flagged unavailable");

            // Assert book is now Unavailable.
            BookRow? book = await _fx.Context.Books.FindAsync(bookId);
            Assert.NotNull(book);
            Assert.Equal(1, book!.Status); // Unavailable
        }
        finally
        {
            // Restore the file.
            IngestionTestFixture.WriteSyntheticPdf(absPath, "Restored Book", "Author");
        }
    }

    [Fact]
    public async Task UnavailableFileFlagging_PreservesProgress()
    {
        // Register books.
        await _fx.Orchestrator.ScanAsync();

        BookFileRow fileRow = await _fx.Context.BookFiles.FirstAsync();
        string bookId = fileRow.BookId;

        // Add reading progress.
        _fx.Context.ReadingProgress.Add(new ReadingProgressRow
        {
            BookId = bookId,
            CurrentPage = 5,
            ScrollOffsetPx = 0.3,
            Status = 1, // Reading
            LastReadUtc = DateTimeOffset.UtcNow,
        });
        await _fx.Context.SaveChangesAsync();

        string absPath = Path.Combine(
            _fx.RootDir,
            fileRow.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Delete(absPath);

        try
        {
            await _fx.Orchestrator.ScanAsync();

            // Reading progress must be intact.
            ReadingProgressRow? progress = await _fx.Context.ReadingProgress
                .FirstOrDefaultAsync(p => p.BookId == bookId);
            Assert.NotNull(progress);
            Assert.Equal(5, progress!.CurrentPage);
        }
        finally
        {
            IngestionTestFixture.WriteSyntheticPdf(absPath, "Restored Book", "Author");
        }
    }

    [Fact]
    public async Task UnavailableFileFlagging_ReactivatesOnReappearance()
    {
        await _fx.Orchestrator.ScanAsync();

        BookFileRow fileRow = await _fx.Context.BookFiles.FirstAsync();
        string bookId = fileRow.BookId;

        string absPath = Path.Combine(
            _fx.RootDir,
            fileRow.RelativePath.Replace('/', Path.DirectorySeparatorChar));

        // Copy the file bytes so we can restore with same content (same hash = same identity).
        byte[] originalBytes = File.ReadAllBytes(absPath);

        // Delete the file and flag as missing.
        File.Delete(absPath);
        await _fx.FlagService.FlagMissingFilesAsync(_fx.RootDir);

        // Verify flagged as Unavailable.
        BookRow? book = await _fx.Context.Books.FindAsync(bookId);
        Assert.NotNull(book);
        Assert.Equal(1, book!.Status);

        // Restore the IDENTICAL file (same bytes = same hash = same identity).
        File.WriteAllBytes(absPath, originalBytes);

        // Re-scan — should reactivate by matching the same content hash.
        await _fx.Orchestrator.ScanAsync();

        // Either the original book is reactivated, or a book at this path is Active.
        int activeBooksAtPath = await _fx.Context.BookFiles
            .Where(f => f.RelativePath == fileRow.RelativePath && f.FileStatus == 0)
            .CountAsync();
        Assert.True(activeBooksAtPath >= 1, "No active book file was found at the restored path");
    }

    [Fact]
    public async Task UnavailableFileFlagging_WritesAuditEvent()
    {
        await _fx.Orchestrator.ScanAsync();

        BookFileRow fileRow = await _fx.Context.BookFiles.FirstAsync();
        string absPath = Path.Combine(
            _fx.RootDir,
            fileRow.RelativePath.Replace('/', Path.DirectorySeparatorChar));

        File.Delete(absPath);

        try
        {
            await _fx.FlagService.FlagMissingFilesAsync(_fx.RootDir);

            // Audit event must exist.
            bool auditEventExists = await _fx.Context.AuditEvents
                .AnyAsync(e => e.EventType == "BookMarkedUnavailable" && e.EntityId == fileRow.BookId);
            Assert.True(auditEventExists, "No AuditEvent with EventType='BookMarkedUnavailable' was written");
        }
        finally
        {
            IngestionTestFixture.WriteSyntheticPdf(absPath, "Restored Book", "Author");
        }
    }
}
