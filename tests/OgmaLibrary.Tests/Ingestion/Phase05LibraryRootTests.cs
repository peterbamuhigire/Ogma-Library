using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>Phase 5 acceptance tests for durable roots, health and relinking.</summary>
public sealed class Phase05LibraryRootTests : IDisposable
{
    private readonly CatalogueDbContext _context = CatalogueTestHelper.CreateInMemoryContext();
    private readonly string _rootA = CreateTempDirectory("ogma-root-a");
    private readonly string _rootB = CreateTempDirectory("ogma-root-b");
    private readonly LibraryRootService _service;

    public Phase05LibraryRootTests()
    {
        _service = new LibraryRootService(
            _context,
            new FileSystemLibraryRootPlatformAdapter());
    }

    public void Dispose()
    {
        _context.Dispose();
        DeleteDirectory(_rootA);
        DeleteDirectory(_rootB);
    }

    [Fact]
    public async Task AddAsync_PersistsCanonicalRootHealthAndStableIdentity()
    {
        LibraryRootDescriptor root = await _service.AddAsync(
            Path.Combine(_rootA, "."),
            "Primary books");

        Assert.NotEqual(default, root.Id);
        Assert.Equal("Primary books", root.DisplayName);
        Assert.Equal(Path.GetFullPath(_rootA), root.CanonicalLocator);
        Assert.Equal(LibraryRootStatus.Available, root.Status);
        Assert.Equal(LibraryRootPermissionStatus.Granted, root.PermissionStatus);
        Assert.True(root.IsEnabled);
        Assert.False(root.AllowSymlinkTraversal);
        Assert.NotNull(root.LastHealthCheckUtc);
        Assert.Single(await _service.ListAsync());
    }

    [Fact]
    public async Task RelinkAsync_PreservesIdentityAndDisableDoesNotDeleteOccurrences()
    {
        LibraryRootDescriptor original = await _service.AddAsync(_rootA);
        _context.FileOccurrences.Add(new FileOccurrenceRow
        {
            FileOccurrenceId = "01PH05OCCURRENCE0000000001",
            LibraryRootId = original.Id.Value,
            RelativePath = "books/example.pdf",
            NormalizedRelativePath = "books/example.pdf",
            AvailabilityStatus = 0,
        });
        await _context.SaveChangesAsync();

        LibraryRootDescriptor relinked = await _service.RelinkAsync(original.Id, _rootB);
        LibraryRootDescriptor disabled = await _service.SetEnabledAsync(original.Id, false);

        Assert.Equal(original.Id, relinked.Id);
        Assert.Equal(Path.GetFullPath(_rootB), relinked.CanonicalLocator);
        Assert.Equal(LibraryRootStatus.Available, relinked.Status);
        Assert.False(disabled.IsEnabled);
        Assert.Equal(original.Id.Value, await _context.FileOccurrences
            .Select(occurrence => occurrence.LibraryRootId)
            .SingleAsync());
    }

    [Fact]
    public async Task RefreshHealthAsync_MissingCompatibilityLocatorRequestsRelink()
    {
        _context.LibraryRoots.Add(new LibraryRootRow
        {
            LibraryRootId = "01PH05COMPATROOT0000000001",
            DisplayName = "Legacy root",
            RootStatus = 0,
            PermissionStatus = 0,
            IsCompatibilityRoot = true,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await _context.SaveChangesAsync();

        LibraryRootDescriptor refreshed = await _service.RefreshHealthAsync(
            new LibraryRootId("01PH05COMPATROOT0000000001"));

        Assert.Equal(LibraryRootStatus.NeedsRelink, refreshed.Status);
        Assert.Equal(LibraryRootPermissionStatus.Unknown, refreshed.PermissionStatus);
        Assert.NotNull(refreshed.LastHealthCheckUtc);
    }

    [Fact]
    public async Task AddAsync_RejectsDuplicateCanonicalLocator()
    {
        await _service.AddAsync(_rootA);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.AddAsync(
            Path.Combine(_rootA, ".")));
    }

    private static string CreateTempDirectory(string prefix)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
