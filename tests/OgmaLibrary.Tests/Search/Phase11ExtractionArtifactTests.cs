using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Search;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Search;

/// <summary>Phase 11 acceptance tests for versioned extraction manifests.</summary>
public sealed class Phase11ExtractionArtifactTests : IDisposable
{
    private readonly CatalogueDbContext _context = CatalogueTestHelper.CreateInMemoryContext();
    private readonly ExtractionArtifactService _artifacts;

    public Phase11ExtractionArtifactTests()
    {
        _context.Books.Add(new BookRow
        {
            BookId = "01PH11BOOK0000000000000001",
            Status = 0,
        });
        _context.SaveChanges();
        _artifacts = new ExtractionArtifactService(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task BeginIsIdempotent_AndCompleteStoresVersionedManifest()
    {
        const string contentHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        ExtractionArtifactDescriptor first = await _artifacts.BeginAsync(
            "01PH11BOOK0000000000000001", contentHash, "pdfium-text-v2");
        ExtractionArtifactDescriptor second = await _artifacts.BeginAsync(
            "01PH11BOOK0000000000000001", contentHash, "pdfium-text-v2");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(ExtractionArtifactStatus.Pending, first.Status);
        ExtractionArtifactDescriptor completed = await _artifacts.CompleteAsync(
            first.Id,
            pagesProcessed: 12,
            failedPages: 1,
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        Assert.Equal(ExtractionArtifactStatus.Completed, completed.Status);
        Assert.Equal(12, completed.PagesProcessed);
        Assert.Equal(1, completed.FailedPages);
        Assert.NotNull(completed.CompletedUtc);
        Assert.Single(await _context.ExtractionArtifacts.ToListAsync());
    }

    [Fact]
    public async Task FailMarksOnlyTheRequestedVersion()
    {
        ExtractionArtifactDescriptor artifact = await _artifacts.BeginAsync(
            "01PH11BOOK0000000000000001", null, "pdfium-text-v3");

        ExtractionArtifactDescriptor failed = await _artifacts.FailAsync(artifact.Id);

        Assert.Equal(ExtractionArtifactStatus.Failed, failed.Status);
        Assert.NotNull(failed.CompletedUtc);
        Assert.Null(failed.ManifestHash);
    }
}
