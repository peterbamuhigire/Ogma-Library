using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Metadata;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Metadata;

/// <summary>Phase 14 acceptance tests for explicit proposal review commands.</summary>
public sealed class Phase14MetadataReviewTests
{
    [Fact]
    public async Task ProposalsRemainPendingUntilExplicitDecision()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();
        const string bookId = "01PH14BOOK0000000000000001";
        context.Books.Add(new BookRow { BookId = bookId, Status = 0 });
        await context.SaveChangesAsync();
        var apply = new MetadataApplyService(context, new MetadataQualityService(context));
        var review = new MetadataReviewService(context, apply);

        IReadOnlyList<MetadataProposalDescriptor> created = await review.CreateAsync(bookId, [
            new MergedMetadataProposal(
                "Title", "Suggested title", null, 0.84, "GoogleBooks",
                [new AlternativeFieldValue("Alternative title", "OpenLibrary", 0.72)]),
            new MergedMetadataProposal("Author", "Suggested author", null, 0.55, "PDF", []),
        ]);

        Assert.Equal(2, created.Count);
        Assert.Equal(2, (await review.ListPendingAsync(bookId)).Count);
        Assert.Empty(context.BookMetadataFields);

        MetadataProposalDescriptor accepted = await review.DecideAsync(
            created[0].Id,
            accept: true,
            editedValue: "Curated title",
            userOverride: true);
        MetadataProposalDescriptor rejected = await review.DecideAsync(created[1].Id, accept: false);

        Assert.Equal(MetadataProposalStatus.Accepted, accepted.Status);
        Assert.Equal(MetadataProposalStatus.Rejected, rejected.Status);
        Assert.Empty(await review.ListPendingAsync(bookId));
        Assert.Equal("Curated title", context.BookMetadataFields.Single().Value);
        Assert.True(context.BookMetadataFields.Single().IsOverridden);
        await Assert.ThrowsAsync<InvalidOperationException>(() => review.DecideAsync(
            created[1].Id, accept: false));
    }
}
