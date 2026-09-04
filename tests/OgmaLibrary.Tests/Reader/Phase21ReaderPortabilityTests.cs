using System.Text;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Tests.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Reader;

namespace OgmaLibrary.Tests.Reader;

/// <summary>Phase 21 tests for versioned reader-state export/import.</summary>
public sealed class Phase21ReaderPortabilityTests : IDisposable
{
    private const string BookId = "PHASE21-PORTABLE-BOOK";
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public Phase21ReaderPortabilityTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.EnsureCreated();
        _context.Books.Add(new BookRow { BookId = BookId, Title = "Portable Book", Status = 0 });
        _context.ReadingProgress.Add(new ReadingProgressRow
        {
            BookId = BookId,
            CurrentPage = 12,
            CompletionPct = 18,
            Status = 1,
        });
        _context.ReadingMemory.Add(new ReadingMemoryRow
        {
            BookId = BookId,
            KeyInsight = "Keep local notes local",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        _context.Bookmarks.Add(new BookmarkRow
        {
            BookmarkId = 2101,
            BookId = BookId,
            Page = 12,
            Label = "Important",
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        _context.AnnotationsV2.Add(new AnnotationV2Row
        {
            AnnotationId = "01JPORTABLEANNOTATION00001",
            BookId = BookId,
            Type = 1,
            RegionsJson = "[{\"p\":12,\"l\":0.1,\"t\":0.2,\"w\":0.3,\"h\":0.1}]",
            NoteText = "Private note",
            CreatedUtc = DateTimeOffset.UtcNow,
            ModifiedUtc = DateTimeOffset.UtcNow,
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task ExportImport_RoundTripsReaderState_Idempotently()
    {
        var service = new ReaderPortabilityService(_context);
        await using var stream = new MemoryStream();

        await service.ExportAsync(BookId, stream);
        Assert.True(stream.Length > 0);
        stream.Position = 0;

        ReaderImportResult first = await service.ImportAsync(BookId, stream);
        stream.Position = 0;
        ReaderImportResult second = await service.ImportAsync(BookId, stream);

        Assert.True(first.ProgressApplied);
        Assert.True(first.ReadingMemoryApplied);
        Assert.Equal(1, first.BookmarksApplied);
        Assert.Equal(1, first.AnnotationsApplied);
        Assert.Equal(first, second);
        Assert.Equal(1, await _context.Bookmarks.CountAsync(bookmark => bookmark.BookId == BookId));
        Assert.Equal(1, await _context.AnnotationsV2.CountAsync(annotation => annotation.BookId == BookId));
    }

    [Fact]
    public async Task Import_RejectsExportForDifferentBook()
    {
        var service = new ReaderPortabilityService(_context);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":1,\"bookId\":\"OTHER-BOOK\",\"bookmarks\":[],\"annotations\":[]}"));

        await Assert.ThrowsAsync<InvalidDataException>(() => service.ImportAsync(BookId, stream));
    }

    [Fact]
    public async Task Import_RejectsMalformedJsonAndExcessiveEntryCount()
    {
        var service = new ReaderPortabilityService(_context);
        await using var malformed = new MemoryStream(Encoding.UTF8.GetBytes("{not-json"));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.ImportAsync(BookId, malformed));

        string oversized = "{\"schemaVersion\":1,\"bookId\":\"" + BookId +
            "\",\"bookmarks\":[" + string.Join(',', Enumerable.Repeat(
                "{\"bookmarkId\":1,\"page\":1}", 10_001)) +
            "],\"annotations\":[]}";
        await using var excessive = new MemoryStream(Encoding.UTF8.GetBytes(oversized));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.ImportAsync(BookId, excessive));
    }

    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }
}
