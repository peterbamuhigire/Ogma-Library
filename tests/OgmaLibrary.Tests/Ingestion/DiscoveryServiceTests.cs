using System.Diagnostics;
using System.Threading.Channels;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Ingestion;
using Xunit.Abstractions;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>
/// Integration tests for <see cref="PdfDiscoveryService"/> (FR-LIB-002).
/// </summary>
public sealed class DiscoveryServiceTests : IDisposable
{
    private readonly IngestionTestFixture _fx = IngestionTestFixture.Create(5);
    private readonly ITestOutputHelper _output;

    public DiscoveryServiceTests(ITestOutputHelper output) => _output = output;

    /// <inheritdoc />
    public void Dispose() => _fx.Dispose();

    [Fact]
    public async Task DiscoveryService_DiscoversPdfs_Recursively()
    {
        var channel = Channel.CreateUnbounded<DiscoveredFile>();
        var svc = new PdfDiscoveryService();

        await svc.DiscoverAsync(
            _fx.RootDir,
            [],
            channel.Writer,
            CancellationToken.None);

        var files = new List<DiscoveredFile>();
        await foreach (var f in channel.Reader.ReadAllAsync())
        {
            files.Add(f);
        }

        // 5 PDFs in books/ and books/subdir/ + 1 in excluded/ = 6 total.
        Assert.Equal(6, files.Count);
    }

    [Fact]
    public async Task DiscoveryService_HonorsExcludedFolders()
    {
        var channel = Channel.CreateUnbounded<DiscoveredFile>();
        var svc = new PdfDiscoveryService();

        await svc.DiscoverAsync(
            _fx.RootDir,
            ["excluded"],
            channel.Writer,
            CancellationToken.None);

        var files = new List<DiscoveredFile>();
        await foreach (var f in channel.Reader.ReadAllAsync())
        {
            files.Add(f);
        }

        // Excluded dir has 1 PDF; should be absent.
        Assert.Equal(5, files.Count);
        Assert.DoesNotContain(files, f => f.RelativePath.Contains("excluded"));
    }

    [Fact]
    public async Task DiscoveryService_PathsNormalized_ForwardSlash()
    {
        var channel = Channel.CreateUnbounded<DiscoveredFile>();
        var svc = new PdfDiscoveryService();

        await svc.DiscoverAsync(
            _fx.RootDir,
            [],
            channel.Writer,
            CancellationToken.None);

        var files = new List<DiscoveredFile>();
        await foreach (var f in channel.Reader.ReadAllAsync())
        {
            files.Add(f);
        }

        // All relative paths must use forward-slash.
        Assert.All(files, f => Assert.DoesNotContain('\\', f.RelativePath));
    }

    [Fact]
    public async Task DiscoveryService_EmitsPerDirectoryLifecycleDiagnostics()
    {
        var channel = Channel.CreateUnbounded<DiscoveredFile>();
        var diagnostics = new List<DiscoveryDirectoryDiagnostic>();
        var svc = new PdfDiscoveryService();

        await svc.DiscoverAsync(
            _fx.RootDir,
            [".settings"],
            channel.Writer,
            diagnostic =>
            {
                diagnostics.Add(diagnostic);
                return ValueTask.CompletedTask;
            },
            cancellationToken: CancellationToken.None);

        await foreach (DiscoveredFile _ in channel.Reader.ReadAllAsync())
        {
        }

        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.RelativeDirectory == string.Empty &&
                          diagnostic.Status == DiscoveryDirectoryStatus.Started);
        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.RelativeDirectory == string.Empty &&
                          diagnostic.Status == DiscoveryDirectoryStatus.Completed);
        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.RelativeDirectory == "books/subdir" &&
                          diagnostic.Status == DiscoveryDirectoryStatus.Completed);
        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.RelativeDirectory.StartsWith(".settings", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Status == DiscoveryDirectoryStatus.Failed);
    }

    [Fact]
    public async Task DiscoveryService_ResumesAfterCompletedDirectory()
    {
        string first = Path.Combine(_fx.RootDir, "a");
        string second = Path.Combine(_fx.RootDir, "b");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        File.WriteAllBytes(Path.Combine(first, "first.pdf"), [1]);
        File.WriteAllBytes(Path.Combine(second, "second.pdf"), [2]);

        var channel = Channel.CreateUnbounded<DiscoveredFile>();
        var svc = new PdfDiscoveryService();
        await svc.DiscoverAsync(
            _fx.RootDir,
            ["books", "excluded", ".settings"],
            channel.Writer,
            resumeAfterRelativeDirectory: "a",
            cancellationToken: CancellationToken.None);

        var files = new List<DiscoveredFile>();
        await foreach (DiscoveredFile file in channel.Reader.ReadAllAsync())
        {
            files.Add(file);
        }

        Assert.Single(files);
        Assert.Equal("b/second.pdf", files[0].RelativePath);
    }

    [Fact]
    public async Task DiscoveryService_ReportsUnreadableDirectoryWithoutAbortingTheScan()
    {
        string missingRoot = Path.Combine(_fx.RootDir, "does-not-exist");
        var channel = Channel.CreateUnbounded<DiscoveredFile>();
        var diagnostics = new List<DiscoveryDirectoryDiagnostic>();
        var svc = new PdfDiscoveryService();

        await svc.DiscoverAsync(
            missingRoot,
            [],
            channel.Writer,
            diagnostic =>
            {
                diagnostics.Add(diagnostic);
                return ValueTask.CompletedTask;
            },
            cancellationToken: CancellationToken.None);

        await foreach (DiscoveredFile _ in channel.Reader.ReadAllAsync())
        {
        }

        DiscoveryDirectoryDiagnostic failure = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Status == DiscoveryDirectoryStatus.Failed);
        Assert.Equal("directory_io_error", failure.ErrorCode);
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public async Task DiscoveryService_EnumeratesFiftyThousandFilesWithBoundedChannel()
    {
        string bulkDirectory = Path.Combine(_fx.RootDir, "bulk");
        Directory.CreateDirectory(bulkDirectory);
        for (int index = 0; index < 50_000; index++)
        {
            File.WriteAllBytes(Path.Combine(bulkDirectory, $"book-{index:D5}.pdf"), []);
        }

        var channel = Channel.CreateBounded<DiscoveredFile>(
            new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.Wait });
        var svc = new PdfDiscoveryService();
        var stopwatch = Stopwatch.StartNew();
        Task discovery = svc.DiscoverAsync(
            _fx.RootDir,
            ["books", "excluded", ".settings"],
            channel.Writer,
            cancellationToken: CancellationToken.None);

        int count = 0;
        await foreach (DiscoveredFile _ in channel.Reader.ReadAllAsync())
        {
            count++;
        }

        await discovery;
        stopwatch.Stop();
        _output.WriteLine($"50,000-file discovery elapsed: {stopwatch.ElapsedMilliseconds} ms");

        Assert.Equal(50_000, count);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(30),
            $"50,000-file discovery exceeded the 30-second CI budget: {stopwatch.Elapsed}.");
    }
}
