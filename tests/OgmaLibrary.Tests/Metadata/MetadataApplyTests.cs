using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Metadata;
using OgmaLibrary.Tests.Catalogue;
using Xunit;

namespace OgmaLibrary.Tests.Metadata;

/// <summary>
/// Integration tests for applying merged metadata and field provenance (FR-META-003,
/// FR-META-004, CTRL-OGMA-018).
/// </summary>
public sealed class MetadataApplyTests
{
    [Fact]
    public async Task MetadataApply_WritesAuditEvent()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow { BookId = "APPLY01", Status = 0 });
        await context.SaveChangesAsync();

        var qualitySvc = new MetadataQualityService(context);
        var svc = new MetadataApplyService(context, qualitySvc);

        var proposals = new[]
        {
            new AcceptedFieldProposal("Title", "Clean Code", "GoogleBooks", 0.85, false),
            new AcceptedFieldProposal("Author", "Robert C. Martin", "GoogleBooks", 0.85, false),
        };

        await svc.ApplyMergedMetadataAsync("APPLY01", proposals);

        var auditEvent = context.AuditEvents
            .SingleOrDefault(e => e.EventType == "MetadataApplied" && e.EntityId == "APPLY01");

        Assert.NotNull(auditEvent);
        Assert.NotNull(auditEvent!.BeforeJson);
        Assert.NotNull(auditEvent.AfterJson);
    }

    [Fact]
    public async Task FieldProvenance_StoredAfterApply()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow { BookId = "APPLY02", Status = 0 });
        await context.SaveChangesAsync();

        var qualitySvc = new MetadataQualityService(context);
        var svc = new MetadataApplyService(context, qualitySvc);

        var proposals = new[]
        {
            new AcceptedFieldProposal("Title", "The Pragmatic Programmer", "GoogleBooks", 0.85, false),
        };

        await svc.ApplyMergedMetadataAsync("APPLY02", proposals);

        var field = context.BookMetadataFields
            .SingleOrDefault(f => f.BookId == "APPLY02" && f.FieldName == "Title");

        Assert.NotNull(field);
        Assert.Equal("The Pragmatic Programmer", field!.Value);
        Assert.Equal("GoogleBooks", field.Source);
        Assert.Equal(0.85, field.Confidence);
        Assert.False(field.IsOverridden);
    }

    [Fact]
    public async Task FieldProvenance_IsOverride_WhenUserEdited()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow { BookId = "APPLY03", Status = 0 });
        await context.SaveChangesAsync();

        var qualitySvc = new MetadataQualityService(context);
        var svc = new MetadataApplyService(context, qualitySvc);

        // User-edited field: IsOverridden = true, Source = "UserOverride".
        var proposals = new[]
        {
            new AcceptedFieldProposal("Title", "My Custom Title", "UserOverride", 1.0, true),
        };

        await svc.ApplyMergedMetadataAsync("APPLY03", proposals);

        var field = context.BookMetadataFields
            .SingleOrDefault(f => f.BookId == "APPLY03" && f.FieldName == "Title");

        Assert.NotNull(field);
        Assert.True(field!.IsOverridden);
        Assert.Equal("UserOverride", field.Source);
        Assert.Equal(1.0, field.Confidence);
    }

    [Fact]
    public async Task MetadataApply_MirrorsCoreFieldsToCatalogueColumns()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow { BookId = "APPLY04", Status = 0 });
        await context.SaveChangesAsync();

        var qualitySvc = new MetadataQualityService(context);
        var svc = new MetadataApplyService(context, qualitySvc);

        var proposals = new[]
        {
            new AcceptedFieldProposal("Title", "Domain-Driven Design", "GoogleBooks", 0.85, false),
            new AcceptedFieldProposal("Author", "Eric Evans", "GoogleBooks", 0.85, false),
            new AcceptedFieldProposal("ISBN", "9780321125217", "GoogleBooks", 0.85, false),
            new AcceptedFieldProposal("Year", "2003", "GoogleBooks", 0.85, false),
        };

        await svc.ApplyMergedMetadataAsync("APPLY04", proposals);

        var book = context.Books.Single(b => b.BookId == "APPLY04");
        Assert.Equal("Domain-Driven Design", book.Title);
        Assert.Equal("9780321125217", book.IsbnNormalized);
        Assert.Equal(2003, book.Year);

        var author = context.Authors.Single();
        Assert.Equal("Eric Evans", author.NormalizedName);

        var bookAuthor = context.BookAuthors.Single();
        Assert.Equal("APPLY04", bookAuthor.BookId);
        Assert.Equal("Author", bookAuthor.Role);
    }

    [Fact]
    public async Task EnrichmentUI_AcceptAll_AppliesOnlyAboveThreshold()
    {
        // Simulate the logic of "Accept all above 0.8": only proposals with
        // MergedConfidence >= 0.8 should be applied.
        var proposals = new List<MergedMetadataProposal>
        {
            new("Title", "High Confidence Title", "Existing Title", 0.90, "GoogleBooks", []),
            new("Author", "Low Confidence Author", null, 0.70, "OpenLibrary", []),
            new("Publisher", "Medium Publisher", null, 0.85, "GoogleBooks", []),
        };

        double threshold = 0.8;
        var accepted = proposals
            .Where(p => p.MergedConfidence >= threshold)
            .Select(p => new AcceptedFieldProposal(p.FieldName, p.ProposedValue, p.WinningProvider, p.MergedConfidence, false))
            .ToList();

        // Only Title and Publisher should pass the 0.8 threshold.
        Assert.Equal(2, accepted.Count);
        Assert.Contains(accepted, a => a.FieldName == "Title");
        Assert.Contains(accepted, a => a.FieldName == "Publisher");
        Assert.DoesNotContain(accepted, a => a.FieldName == "Author");
    }
}

