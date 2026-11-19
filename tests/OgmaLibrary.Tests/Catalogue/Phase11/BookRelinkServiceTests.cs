using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Ingestion;
using Xunit;

namespace OgmaLibrary.Tests.Catalogue.Phase11;

/// <summary>
/// Sept-23 Phase 11 slice 6 (task 11.7): per-book relink accepts only a file inside an enabled
/// library root whose SHA-256 matches the catalogue, and records an audit event.
/// </summary>
public sealed class BookRelinkServiceTests : IDisposable
{
    private const string BookId = "relink-book";
    private static readonly byte[] Content = "%PDF-1.4 synthetic relink content"u8.ToArray();

    private readonly string _temp = Path.Combine(Path.GetTempPath(), $"ogma-p11-relink-{Guid.NewGuid():N}");
    private readonly CatalogueDbContext _context = CatalogueTestHelper.CreateInMemoryContext();
    private readonly string _root;
    private string _rootId = string.Empty;

    public BookRelinkServiceTests()
    {
        _root = Path.Combine(_temp, "Library");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        _context.Dispose();
        try
        {
            Directory.Delete(_temp, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup of the temporary library.
        }
    }

    [Fact]
    public async Task MatchingHashInsideRoot_RelinksFileRowAndRecordsAudit()
    {
        await SeedAsync();
        string moved = WriteFile(Path.Combine(_root, "moved", "renamed.pdf"), Content);

        BookRelinkResult result = await Service().RelinkAsync(BookId, moved);

        Assert.Equal(BookRelinkResult.Relinked, result);
        BookFileRow file = await _context.BookFiles.AsNoTracking().SingleAsync(f => f.BookId == BookId);
        Assert.Equal(_rootId, file.LibraryRootId);
        Assert.Equal("moved/renamed.pdf", file.RelativePath);
        Assert.Equal(0, file.FileStatus);
        BookRow book = await _context.Books.AsNoTracking().SingleAsync(b => b.BookId == BookId);
        Assert.Equal(0, book.Status);
        AuditEventRow audit = await _context.AuditEvents.AsNoTracking()
            .SingleAsync(e => e.EventType == "BookFileRelinked");
        Assert.Equal(BookId, audit.EntityId);
        Assert.DoesNotContain("renamed", audit.AfterJson ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HashMismatch_IsRefusedAndCatalogueUnchanged()
    {
        await SeedAsync();
        string other = WriteFile(Path.Combine(_root, "other.pdf"), "%PDF-1.4 different"u8.ToArray());

        BookRelinkResult result = await Service().RelinkAsync(BookId, other);

        Assert.Equal(BookRelinkResult.HashMismatch, result);
        await AssertUnchangedAsync();
    }

    [Fact]
    public async Task FileOutsideEnabledRoots_IsRefused()
    {
        await SeedAsync();
        string outside = WriteFile(Path.Combine(_temp, "Elsewhere", "copy.pdf"), Content);

        BookRelinkResult result = await Service().RelinkAsync(BookId, outside);

        Assert.Equal(BookRelinkResult.OutsideLibraryRoots, result);
        await AssertUnchangedAsync();
    }

    [Fact]
    public async Task FileInsideDisabledRoot_IsRefused()
    {
        await SeedAsync();
        LibraryRootRow root = await _context.LibraryRoots.SingleAsync(r => r.LibraryRootId == _rootId);
        root.IsEnabled = false;
        await _context.SaveChangesAsync();
        string moved = WriteFile(Path.Combine(_root, "moved.pdf"), Content);

        BookRelinkResult result = await Service().RelinkAsync(BookId, moved);

        Assert.Equal(BookRelinkResult.OutsideLibraryRoots, result);
    }

    [Fact]
    public async Task TraversalOutOfRoot_IsRefused()
    {
        await SeedAsync();
        WriteFile(Path.Combine(_temp, "escape.pdf"), Content);
        string traversal = Path.Combine(_root, "..", "escape.pdf");

        BookRelinkResult result = await Service().RelinkAsync(BookId, traversal);

        Assert.Equal(BookRelinkResult.OutsideLibraryRoots, result);
        await AssertUnchangedAsync();
    }

    [Fact]
    public async Task MissingPath_ReturnsNotFound()
    {
        await SeedAsync();

        BookRelinkResult result = await Service().RelinkAsync(BookId, Path.Combine(_root, "nowhere.pdf"));

        Assert.Equal(BookRelinkResult.NotFound, result);
        await AssertUnchangedAsync();
    }

    [Fact]
    public async Task UnknownBook_ReturnsNotFound()
    {
        await SeedAsync();
        string moved = WriteFile(Path.Combine(_root, "moved.pdf"), Content);

        BookRelinkResult result = await Service().RelinkAsync("no-such-book", moved);

        Assert.Equal(BookRelinkResult.NotFound, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative/book.pdf")]
    public async Task UnqualifiedPath_IsRefused(string path)
    {
        await SeedAsync();

        BookRelinkResult result = await Service().RelinkAsync(BookId, path);

        Assert.Equal(BookRelinkResult.OutsideLibraryRoots, result);
    }

    private BookRelinkService Service() => new(_context);

    private async Task SeedAsync()
    {
        var roots = new LibraryRootService(_context, new FileSystemLibraryRootPlatformAdapter());
        _rootId = (await roots.AddAsync(_root)).Id.Value;
        _context.Books.Add(new BookRow
        {
            BookId = BookId,
            Title = "Synthetic relink",
            RelativePath = "original.pdf",
            Sha256Hash = Convert.ToHexStringLower(SHA256.HashData(Content)),
            Status = 1,
        });
        _context.BookFiles.Add(new BookFileRow
        {
            BookId = BookId,
            LibraryRootId = _rootId,
            RelativePath = "original.pdf",
            FileStatus = 1,
            LastSeenUtc = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    private async Task AssertUnchangedAsync()
    {
        BookFileRow file = await _context.BookFiles.AsNoTracking().SingleAsync(f => f.BookId == BookId);
        Assert.Equal("original.pdf", file.RelativePath);
        Assert.Equal(1, file.FileStatus);
        Assert.False(await _context.AuditEvents.AnyAsync(e => e.EventType == "BookFileRelinked"));
    }

    private static string WriteFile(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
