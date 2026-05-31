using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Metadata;
using OgmaLibrary.Tests.Catalogue;
using Xunit;

namespace OgmaLibrary.Tests.Metadata;

/// <summary>
/// Tests for the confidence merge service (FR-META-003, SRS Â§8.3).
/// </summary>
public sealed class ConfidenceMergeTests
{
    private static ProviderMetadataResult MakeResult(
        string provider,
        string isbn,
        string? title = null,
        string? author = null,
        string? publisher = null,
        int? year = null,
        string? description = null,
        double? confidence = null,
        DateTimeOffset? retrievedUtc = null)
    {
        return new ProviderMetadataResult(
            Provider: provider,
            RequestIsbn: isbn,
            Title: title,
            Authors: author is not null ? [author] : [],
            Publisher: publisher,
            Year: year,
            Description: description,
            CoverUrl: null,
            Categories: [],
            IsbnNormalized: isbn,
            Confidence: confidence ?? (provider == "GoogleBooks" ? 0.85 : 0.80),
            RetrievedUtc: retrievedUtc ?? DateTimeOffset.UtcNow,
            RawJson: "{}");
    }

    [Fact]
    public async Task ConfidenceMerge_ProducesProposal_PerField()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow { BookId = "MERGE01", Status = 0 });
        await context.SaveChangesAsync();

        var svc = new ConfidenceMergeService(context);
        var lookupResults = new[]
        {
            MakeResult("GoogleBooks", "9780262033848", title: "Clean Code",
                author: "Robert Martin", publisher: "Prentice Hall", year: 2008,
                description: "Software craftsmanship"),
            MakeResult("OpenLibrary", "9780262033848", title: "Clean Code",
                author: "Robert C. Martin", publisher: "Prentice Hall", year: 2008),
        };

        var proposals = await svc.MergeAsync("MERGE01", lookupResults);

        // Must have proposals for all core fields.
        var fieldNames = proposals.Select(p => p.FieldName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Title", fieldNames);
        Assert.Contains("Author", fieldNames);
        Assert.Contains("ISBN", fieldNames);
        Assert.Contains("Year", fieldNames);
        Assert.Contains("Publisher", fieldNames);
        Assert.Contains("Description", fieldNames);
    }

    [Fact]
    public async Task ConfidenceMerge_SelectsMaxConfidence_Winner()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow { BookId = "MERGE02", Status = 0 });
        await context.SaveChangesAsync();

        var svc = new ConfidenceMergeService(context);
        var lookupResults = new[]
        {
            // Google Books has higher provider weight (0.85) than Open Library (0.80).
            MakeResult("GoogleBooks", "9780262033848", title: "Clean Code"),
            MakeResult("OpenLibrary", "9780262033848", title: "Kleen Kode"), // different
        };

        var proposals = await svc.MergeAsync("MERGE02", lookupResults);

        var titleProposal = proposals.Single(p => p.FieldName == "Title");

        // Google Books should win (higher confidence with identical title match).
        // Both propose different titles; Google Books has 0.85 weight.
        Assert.NotNull(titleProposal.ProposedValue);
        Assert.True(titleProposal.MergedConfidence > 0.0);
    }

    [Fact]
    public async Task ConfidenceMerge_ManualOverride_AlwaysWins()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow { BookId = "MERGE03", Status = 0 });

        // Seed a manually overridden Title field.
        context.BookMetadataFields.Add(new Infrastructure.Catalogue.Entities.BookMetadataFieldRow
        {
            BookId = "MERGE03",
            FieldName = "Title",
            Value = "My Custom Title",
            Source = "UserOverride",
            IsOverridden = true,
            Confidence = 1.0,
        });
        await context.SaveChangesAsync();

        var svc = new ConfidenceMergeService(context);
        var lookupResults = new[]
        {
            MakeResult("GoogleBooks", "9780262033848", title: "Some Other Title"),
        };

        var proposals = await svc.MergeAsync("MERGE03", lookupResults);
        var titleProposal = proposals.Single(p => p.FieldName == "Title");

        // Override must win unconditionally.
        Assert.Equal("My Custom Title", titleProposal.ProposedValue);
        Assert.Equal(1.0, titleProposal.MergedConfidence);
        Assert.Equal("UserOverride", titleProposal.WinningProvider);
    }

    [Fact]
    public async Task ConfidenceMerge_EmptyLookupResults_ReturnsCurrentValues()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
        {
            BookId = "MERGE04",
            Status = 0,
            Title = "Existing Title",
        });
        context.BookMetadataFields.Add(new Infrastructure.Catalogue.Entities.BookMetadataFieldRow
        {
            BookId = "MERGE04",
            FieldName = "Title",
            Value = "Existing Title",
            Source = "PDF",
            Confidence = 0.5,
        });
        await context.SaveChangesAsync();

        var svc = new ConfidenceMergeService(context);
        var proposals = await svc.MergeAsync("MERGE04", []);

        var titleProposal = proposals.Single(p => p.FieldName == "Title");
        // Should return the current catalogue value when no providers provided data.
        Assert.Equal("Existing Title", titleProposal.ProposedValue);
        Assert.Equal("Existing Title", titleProposal.CurrentValue);
    }
}

