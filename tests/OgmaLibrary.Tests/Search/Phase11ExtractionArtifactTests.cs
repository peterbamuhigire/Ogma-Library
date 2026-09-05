using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Metadata;
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

    [Fact]
    public async Task IsbnEvidenceStore_ReplacesRankedSourceEvidenceWithoutChangingBookMetadata()
    {
        var store = new IsbnEvidenceStore(_context);
        ExtractionArtifactDescriptor artifact = await _artifacts.BeginAsync(
            "01PH11BOOK0000000000000001", null, "pdf-text-v4");
        var candidates = new IsbnCandidate[]
        {
            new(ParseIsbn("9780262033848"), IsbnSource.DocInfo),
            new(ParseIsbn("0262033844"), IsbnSource.FirstPage),
        };

        await store.ReplaceAsync(
            artifact.BookId,
            artifact.Id,
            candidates);

        List<ExtractedIsbnEvidenceRow> rows = await _context.ExtractedIsbnEvidence
            .OrderBy(row => row.Rank)
            .ToListAsync();

        Assert.Collection(
            rows,
            first =>
            {
                Assert.Equal("9780262033848", first.IsbnNormalized);
                Assert.Equal(0, first.Source);
                Assert.Equal(0, first.Rank);
                Assert.True(first.IsBest);
            },
            second =>
            {
                Assert.Equal("0262033844", second.IsbnNormalized);
                Assert.Equal(2, second.Source);
                Assert.Equal(1, second.Rank);
                Assert.False(second.IsBest);
            });
        Assert.Null(_context.Books.Single().IsbnNormalized);

        await store.ReplaceAsync(artifact.BookId, artifact.Id, [candidates[1]]);
        Assert.Single(await _context.ExtractedIsbnEvidence.ToListAsync());
        Assert.Equal("0262033844", (await _context.ExtractedIsbnEvidence.SingleAsync()).IsbnNormalized);
    }

    private static Isbn ParseIsbn(string value) =>
        Isbn.TryParse(value, out Isbn isbn)
            ? isbn
            : throw new InvalidOperationException("The test ISBN must be valid.");
}
