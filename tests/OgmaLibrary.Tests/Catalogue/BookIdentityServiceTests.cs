using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>
/// Unit tests for <see cref="BookIdentityService"/> covering the five-tier identity
/// chain (HLD §4, FR-LIB-003, Phase 04 deliverable 10).
/// Tests use a real temp SQLite file for the DbContext.
/// </summary>
public sealed class BookIdentityServiceTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;
    private readonly string _tempRoot;
    private readonly BookIdentityService _service;

    public BookIdentityServiceTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.Migrate();
        _service = new BookIdentityService(_context);
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ogma-identity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task BookIdentityService_ResolvesAfterRename()
    {
        // Arrange — write a file, compute its hash, register it in the catalogue.
        string content = "This is the content of the book for rename test.";
        string originalPath = Path.Combine(_tempRoot, "original.pdf");
        await File.WriteAllTextAsync(originalPath, content);

        byte[] contentBytes = System.Text.Encoding.UTF8.GetBytes(content);
        byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(contentBytes);
        string sha256Hex = Convert.ToHexStringLower(hashBytes);

        // Register in catalogue with original relative path.
        _context.Books.Add(new BookRow
        {
            BookId = "01HZIDTESTRENAMEAAAAAAAAAAA",
            Title = "Rename Test Book",
            Sha256Hash = sha256Hex,
            Status = 0,
        });
        await _context.SaveChangesAsync();

        // Act — rename the file and resolve again.
        string renamedPath = Path.Combine(_tempRoot, "renamed.pdf");
        File.Move(originalPath, renamedPath);

        BookMatchResult result = await _service.ResolveAsync(renamedPath, _tempRoot);

        // Assert — tier 2 (SHA-256) finds the book even though the path changed.
        var exactMatch = Assert.IsType<BookMatchResult.ExactMatch>(result);
        Assert.Equal("01HZIDTESTRENAMEAAAAAAAAAAA", exactMatch.BookId);
    }

    [Fact]
    public async Task BookIdentityService_ResolvesAfterMove()
    {
        // Arrange — write a file and register it.
        string content = "Content for move test.";
        string originalPath = Path.Combine(_tempRoot, "moveme.pdf");
        await File.WriteAllTextAsync(originalPath, content);

        byte[] contentBytes = System.Text.Encoding.UTF8.GetBytes(content);
        byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(contentBytes);
        string sha256Hex = Convert.ToHexStringLower(hashBytes);

        _context.Books.Add(new BookRow
        {
            BookId = "01HZIDTESTMOVEAAAAAAAAAAAAA",
            Title = "Move Test Book",
            Sha256Hash = sha256Hex,
            Status = 0,
        });
        await _context.SaveChangesAsync();

        // Move to a sub-folder.
        string subFolder = Path.Combine(_tempRoot, "subfolder");
        Directory.CreateDirectory(subFolder);
        string movedPath = Path.Combine(subFolder, "moveme.pdf");
        File.Move(originalPath, movedPath);

        // Act.
        BookMatchResult result = await _service.ResolveAsync(movedPath, _tempRoot);

        // Assert.
        var exactMatch = Assert.IsType<BookMatchResult.ExactMatch>(result);
        Assert.Equal("01HZIDTESTMOVEAAAAAAAAAAAAA", exactMatch.BookId);
    }

    [Fact]
    public async Task BookIdentityService_FallsBackToHash()
    {
        // Arrange — write a file and register it only with a SHA-256 hash (no path).
        string content = "Fallback to hash test content.";
        string filePath = Path.Combine(_tempRoot, "hashonly.pdf");
        await File.WriteAllTextAsync(filePath, content);

        byte[] contentBytes = System.Text.Encoding.UTF8.GetBytes(content);
        byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(contentBytes);
        string sha256Hex = Convert.ToHexStringLower(hashBytes);

        _context.Books.Add(new BookRow
        {
            BookId = "01HZIDTESTHASHFBAAAAAAAAAAA",
            Title = "Hash Fallback Book",
            Sha256Hash = sha256Hex,
            Status = 0,
        });
        await _context.SaveChangesAsync();

        // Act — no BookFile record, so tier 1 misses; tier 2 (hash) should match.
        BookMatchResult result = await _service.ResolveAsync(filePath, _tempRoot);

        // Assert.
        var exactMatch = Assert.IsType<BookMatchResult.ExactMatch>(result);
        Assert.Equal("01HZIDTESTHASHFBAAAAAAAAAAA", exactMatch.BookId);
    }

    [Fact]
    public async Task BookIdentityService_ReturnsNewBook_ForUnknownFile()
    {
        // Arrange — a file that has never been imported.
        string filePath = Path.Combine(_tempRoot, "unknown.pdf");
        await File.WriteAllTextAsync(filePath, "Brand new content never seen before.");

        // Act.
        BookMatchResult result = await _service.ResolveAsync(filePath, _tempRoot);

        // Assert — NewBook because no match at any tier.
        Assert.IsType<BookMatchResult.NewBook>(result);
    }

    [Fact]
    public async Task BookIdentityService_ReturnsUnresolvable_ForMissingFile()
    {
        string missingPath = Path.Combine(_tempRoot, "does-not-exist.pdf");

        BookMatchResult result = await _service.ResolveAsync(missingPath, _tempRoot);

        Assert.IsType<BookMatchResult.Unresolvable>(result);
    }

    [Fact]
    public async Task BookIdentity_RematchesAfterRename()
    {
        // Alias test to satisfy deliverable naming (FR-LIB-003).
        string content = "Content for rematching test.";
        string original = Path.Combine(_tempRoot, "book-a.pdf");
        await File.WriteAllTextAsync(original, content);

        byte[] hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(content));
        string sha256 = Convert.ToHexStringLower(hash);

        _context.Books.Add(new BookRow
        {
            BookId = "01HZIDTESTREMATCHAAAAAAAAA1",
            Sha256Hash = sha256,
            Status = 0,
        });
        await _context.SaveChangesAsync();

        string renamed = Path.Combine(_tempRoot, "book-b.pdf");
        File.Move(original, renamed);

        BookMatchResult result = await _service.ResolveAsync(renamed, _tempRoot);

        Assert.IsType<BookMatchResult.ExactMatch>(result);
    }
}
