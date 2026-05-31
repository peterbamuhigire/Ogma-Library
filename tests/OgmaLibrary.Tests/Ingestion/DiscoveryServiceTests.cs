using System.Threading.Channels;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Ingestion;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>
/// Integration tests for <see cref="PdfDiscoveryService"/> (FR-LIB-002).
/// </summary>
public sealed class DiscoveryServiceTests : IDisposable
{
    private readonly IngestionTestFixture _fx = IngestionTestFixture.Create(5);

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
}
