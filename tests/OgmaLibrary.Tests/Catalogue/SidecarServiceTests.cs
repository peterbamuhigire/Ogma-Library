using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Pathing;
using OgmaLibrary.Infrastructure.Sidecar;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>
/// Unit tests for <see cref="SidecarService"/> verifying the SHA-256 sharded path
/// convention (Phase 04 WP5, ADR-0005).
/// </summary>
public sealed class SidecarServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly SidecarService _service;

    public SidecarServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ogma-sidecar-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
        _service = new SidecarService(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private const string SampleHash = "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";

    [Fact]
    public void SidecarService_ResolvesPath_MatchesConvention()
    {
        // The relative path for a cover should follow:
        // .ogma/covers/<hash[0:2]>/<hash>.jpg
        string relative = _service.ResolveRelative(SampleHash, SidecarClass.Covers);

        Assert.Equal($".ogma/covers/ab/{SampleHash}.jpg", relative);
    }

    [Fact]
    public void SidecarService_ResolveRelative_UsesForwardSlashes()
    {
        // Relative paths must always use forward-slash separators for portability.
        string relative = _service.ResolveRelative(SampleHash, SidecarClass.Thumbnails, "_p003");

        Assert.DoesNotContain('\\', relative);
        Assert.Contains('/', relative);
    }

    [Fact]
    public void SidecarService_Resolve_ReturnsAbsolutePath()
    {
        string absolute = _service.Resolve(SampleHash, SidecarClass.Covers);

        Assert.True(Path.IsPathRooted(absolute));
        Assert.StartsWith(
            PathGuard.CanonicalizeRoot(_tempRoot),
            absolute,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SidecarService_CreatesDirectories_OnFirstAccess()
    {
        string absolute = _service.Resolve(SampleHash, SidecarClass.Spines);
        string? directory = Path.GetDirectoryName(absolute);

        Assert.NotNull(directory);
        Assert.True(Directory.Exists(directory));
    }

    [Theory]
    [InlineData(SidecarClass.Covers, "jpg")]
    [InlineData(SidecarClass.Thumbnails, "jpg")]
    [InlineData(SidecarClass.Spines, "jpg")]
    [InlineData(SidecarClass.Ocr, "hocr")]
    [InlineData(SidecarClass.ExtractedText, "json.gz")]
    [InlineData(SidecarClass.Embeddings, "bin")]
    [InlineData(SidecarClass.Backups, "pdf")]
    [InlineData(SidecarClass.Export, "zip")]
    [InlineData(SidecarClass.CitationExports, "txt")]
    public void SidecarService_AllClasses_HaveCorrectExtension(SidecarClass cls, string expectedExtension)
    {
        string relative = _service.ResolveRelative(SampleHash, cls);

        Assert.EndsWith($".{expectedExtension}", relative);
    }

    [Fact]
    public void SidecarService_VariantSuffix_IsIncludedInPath()
    {
        string relative = _service.ResolveRelative(SampleHash, SidecarClass.Thumbnails, "_p099");

        Assert.Contains("_p099", relative);
    }

    [Fact]
    public void SidecarService_ShardPrefix_IsFirstTwoCharsOfHash()
    {
        // Shard prefix is "ab" for our sample hash.
        string relative = _service.ResolveRelative(SampleHash, SidecarClass.Covers);

        Assert.Contains("/ab/", relative);
    }
}
