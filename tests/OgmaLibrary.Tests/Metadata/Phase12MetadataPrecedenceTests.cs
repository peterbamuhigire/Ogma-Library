using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Metadata;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Metadata;

/// <summary>Phase 12 acceptance tests for canonical metadata precedence.</summary>
public sealed class Phase12MetadataPrecedenceTests
{
    [Theory]
    [InlineData("Title", MetadataFieldScope.Work)]
    [InlineData("Author", MetadataFieldScope.Work)]
    [InlineData("Description", MetadataFieldScope.Work)]
    [InlineData("ISBN", MetadataFieldScope.Edition)]
    [InlineData("Publisher", MetadataFieldScope.Edition)]
    public void MetadataFieldPolicy_AssignsStableCanonicalScope(string field, MetadataFieldScope expected)
    {
        Assert.Equal(expected, MetadataFieldPolicy.ScopeFor(field));
    }

    [Fact]
    public async Task ProviderProposalCannotOverwriteUserOverride()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new BookRow { BookId = "01PH12BOOK0000000000000001", Status = 0 });
        context.BookMetadataFields.Add(new BookMetadataFieldRow
        {
            BookId = "01PH12BOOK0000000000000001",
            FieldName = "Title",
            Value = "Curated title",
            Source = "UserOverride",
            Confidence = 1.0,
            IsOverridden = true,
        });
        await context.SaveChangesAsync();
        var service = new MetadataApplyService(
            context,
            new MetadataQualityService(context));

        await service.ApplyMergedMetadataAsync(
            "01PH12BOOK0000000000000001",
            [new AcceptedFieldProposal("Title", "Provider title", "GoogleBooks", 0.95, false)]);

        Assert.Equal("Curated title", context.BookMetadataFields.Single().Value);
        Assert.True(context.BookMetadataFields.Single().IsOverridden);
    }

    [Fact]
    public async Task ExplicitUserOverrideCanReplacePriorCuration()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new BookRow { BookId = "01PH12BOOK0000000002", Status = 0 });
        await context.SaveChangesAsync();
        var service = new MetadataApplyService(
            context,
            new MetadataQualityService(context));

        await service.ApplyMergedMetadataAsync(
            "01PH12BOOK0000000002",
            [new AcceptedFieldProposal("Title", "User title", "UserOverride", 1.0, true)]);
        await service.ApplyMergedMetadataAsync(
            "01PH12BOOK0000000002",
            [new AcceptedFieldProposal("Title", "Provider title", "GoogleBooks", 0.95, false)]);

        Assert.Equal("User title", context.BookMetadataFields.Single().Value);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ApplyMergedMetadataAsync(
            "01PH12BOOK0000000002",
            [new AcceptedFieldProposal("Title", "Invalid override", "GoogleBooks", 0.9, true)]));
    }
}
