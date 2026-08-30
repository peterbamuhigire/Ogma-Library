using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>Phase 9 acceptance tests for conservative durable identity decisions.</summary>
public sealed class Phase09IdentityDecisionTests : IDisposable
{
    private readonly CatalogueDbContext _context = CatalogueTestHelper.CreateInMemoryContext();
    private readonly IdentityDecisionRepository _decisions;
    private readonly FileOccurrenceId _left = new("01PH09OCCURRENCE0000000001");
    private readonly FileOccurrenceId _right = new("01PH09OCCURRENCE0000000002");

    public Phase09IdentityDecisionTests()
    {
        _context.LibraryRoots.Add(new LibraryRootRow
        {
            LibraryRootId = "01PH09ROOT0000000000000001",
            DisplayName = "Phase 9 root",
            RootStatus = 0,
            PermissionStatus = 1,
            IsEnabled = true,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        _context.FileOccurrences.AddRange(
            NewOccurrence(_left, "left.pdf"),
            NewOccurrence(_right, "right.pdf"));
        _context.SaveChanges();
        _decisions = new IdentityDecisionRepository(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task ExactHashDecision_IsAutomaticAndIdempotentlyPersisted()
    {
        const string hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        IdentityEvidenceProfile left = new(_left, new ContentHash(hash));
        IdentityEvidenceProfile right = new(_right, new ContentHash(hash));

        IdentityDecision first = await _decisions.EvaluateAndRecordAsync(left, right);
        IdentityDecision second = await _decisions.EvaluateAndRecordAsync(left, right);

        Assert.Equal(IdentityRelationship.ExactContentCopy, first.Relationship);
        Assert.Equal(IdentityDecisionDisposition.Automatic, first.Disposition);
        Assert.Equal(first.Id, second.Id);
        Assert.Single(await _context.IdentityDecisions.ToListAsync());
        Assert.Empty(await _decisions.ListReviewRequiredAsync());
    }

    [Fact]
    public async Task SharedEditionIdentifier_IsReviewRequiredAndNeverAutoMerged()
    {
        Assert.True(Isbn.TryParse("9780306406157", out Isbn isbn));
        var identifier = new BibliographicIdentifier(
            "isbn", BibliographicIdentifierKind.Isbn13,
            BibliographicIdentityScope.Edition, isbn.Normalized);
        IdentityDecision decision = await _decisions.EvaluateAndRecordAsync(
            new IdentityEvidenceProfile(_left, null, [identifier]),
            new IdentityEvidenceProfile(_right, null, [identifier]));

        Assert.Equal(IdentityRelationship.SameEditionDifferentAsset, decision.Relationship);
        Assert.Equal(IdentityDecisionDisposition.ReviewRequired, decision.Disposition);
        Assert.Single(await _decisions.ListReviewRequiredAsync());
    }

    private static FileOccurrenceRow NewOccurrence(FileOccurrenceId id, string path) => new()
    {
        FileOccurrenceId = id.Value,
        LibraryRootId = "01PH09ROOT0000000000000001",
        RelativePath = path,
        NormalizedRelativePath = path,
        AvailabilityStatus = 0,
    };
}
