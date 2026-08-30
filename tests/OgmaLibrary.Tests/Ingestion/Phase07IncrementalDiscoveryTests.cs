using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>Phase 7 acceptance tests for restartable incremental discovery.</summary>
public sealed class Phase07IncrementalDiscoveryTests : IDisposable
{
    private readonly CatalogueDbContext _context = CatalogueTestHelper.CreateInMemoryContext();
    private readonly string _rootPath = CreateRoot();
    private readonly IncrementalDiscoveryService _scanner;

    public Phase07IncrementalDiscoveryTests()
    {
        var roots = new LibraryRootService(
            _context,
            new FileSystemLibraryRootPlatformAdapter());
        var processing = new ProcessingStateService(_context);
        _scanner = new IncrementalDiscoveryService(
            _context,
            roots,
            new PdfDiscoveryService(),
            processing);
    }

    public void Dispose()
    {
        _context.Dispose();
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task Rescan_QueuesOnlyChangedFiles_AndAdvancesRootCheckpoint()
    {
        File.WriteAllBytes(Path.Combine(_rootPath, "one.pdf"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(_rootPath, "two.pdf"), [4, 5, 6]);
        var rootService = new LibraryRootService(
            _context,
            new FileSystemLibraryRootPlatformAdapter());
        LibraryRootDescriptor root = await rootService.AddAsync(_rootPath);

        DiscoveryScanResult first = await _scanner.ScanAsync(root.Id);
        DiscoveryScanResult second = await _scanner.ScanAsync(root.Id);

        Assert.Equal(2, first.FilesSeen);
        Assert.Equal(2, first.ChangedFiles);
        Assert.Equal(0, first.UnchangedFiles);
        Assert.Equal(2, second.FilesSeen);
        Assert.Equal(0, second.ChangedFiles);
        Assert.Equal(2, second.UnchangedFiles);
        Assert.Equal(2, await _context.DiscoveryObservations.CountAsync());
        Assert.Equal(2, await _context.StageExecutions.CountAsync());
        Assert.Single(await _context.DirectoryCheckpoints.ToListAsync());
        Assert.NotNull((await rootService.ListAsync()).Single().LastSuccessfulScanUtc);
    }

    [Fact]
    public async Task Scan_IsRootScoped_AndExcludedFoldersDoNotCreateObservations()
    {
        string excluded = Path.Combine(_rootPath, "private");
        Directory.CreateDirectory(excluded);
        File.WriteAllBytes(Path.Combine(_rootPath, "included.pdf"), [1]);
        File.WriteAllBytes(Path.Combine(excluded, "excluded.pdf"), [2]);
        var rootService = new LibraryRootService(
            _context,
            new FileSystemLibraryRootPlatformAdapter());
        LibraryRootDescriptor root = await rootService.AddAsync(_rootPath);

        DiscoveryScanResult result = await _scanner.ScanAsync(
            root.Id,
            ["private"]);

        Assert.Equal(1, result.FilesSeen);
        Assert.Equal("included.pdf", await _context.DiscoveryObservations
            .Select(observation => observation.NormalizedRelativePath)
            .SingleAsync());
        Assert.Equal(root.Id.Value, await _context.DiscoveryObservations
            .Select(observation => observation.LibraryRootId)
            .SingleAsync());
    }

    private static string CreateRoot()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ogma-phase07-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
