using System.Text.Json;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Metadata;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Metadata;

public sealed class Phase13ConflictAggregationTests
{
    [Fact]
    public void MetadataConflictDetector_IgnoresFormattingAndFailures_ButReportsDifferentValues()
    {
        var detector = new MetadataConflictDetector();
        MetadataConflictReport report = detector.Detect(
        [
            Result("GoogleBooks", "Clean  Code", ["Robert C. Martin", "Uncle Bob"], "978-0132350884"),
            Result("OpenLibrary", "Clean Coder", ["Uncle Bob", "Robert C. Martin"], "9780132350884"),
            Result("Other", "Different Code", [], "9780132350884", confidence: 0.0),
        ]);

        MetadataFieldConflict titleConflict = Assert.Single(report.Conflicts);
        Assert.Equal("Title", titleConflict.FieldName);
        Assert.Equal(["GoogleBooks", "OpenLibrary"], titleConflict.Candidates
            .Select(candidate => candidate.Provider)
            .ToArray());
    }

    [Fact]
    public async Task MetadataProviderAggregator_PersistsPrivacySafeConflictAudit()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow { BookId = "CONFLICT01", Status = 0 });
        await context.SaveChangesAsync();

        var aggregator = new MetadataProviderAggregator(
            [
                new FixedMetadataProvider("GoogleBooks", Result("GoogleBooks", "Clean Code", [], "9780132350884")),
                new FixedMetadataProvider("OpenLibrary", Result("OpenLibrary", "Clean Coder", [], "9780132350884")),
            ],
            context);

        IReadOnlyList<ProviderMetadataResult> results = await aggregator.AggregateAsync(
            "CONFLICT01",
            "9780132350884");

        Assert.Equal(2, results.Count);
        AuditEventRow audit = Assert.Single(context.AuditEvents
            .Where(eventRow => eventRow.EventType == "ProviderConflict"));
        Assert.NotNull(audit.AfterJson);
        using JsonDocument document = JsonDocument.Parse(audit.AfterJson!);
        Assert.False(document.RootElement.GetProperty("containsRawValues").GetBoolean());
        Assert.DoesNotContain("Clean Code", audit.AfterJson, StringComparison.Ordinal);
        Assert.Equal("Title", document.RootElement.GetProperty("fields")[0]
            .GetProperty("field").GetString());
    }

    private static ProviderMetadataResult Result(
        string provider,
        string title,
        IReadOnlyList<string> authors,
        string isbn,
        double confidence = 0.9) => new(
            Provider: provider,
            RequestIsbn: isbn,
            Title: title,
            Authors: authors,
            Publisher: null,
            Year: null,
            Description: null,
            CoverUrl: null,
            Categories: [],
            IsbnNormalized: isbn,
            Confidence: confidence,
            RetrievedUtc: DateTimeOffset.UtcNow,
            RawJson: "{\"title\":\"private provider response\"}");

    private sealed class FixedMetadataProvider(
        string ProviderName,
        ProviderMetadataResult result) : IMetadataProvider
    {
        public string ProviderName { get; } = ProviderName;

        public Task<ProviderMetadataResult?> LookupAsync(
            string isbn13,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ProviderMetadataResult?>(result);

        public Task<IReadOnlyList<ProviderMetadataResult>> SearchAsync(
            MetadataLookupRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProviderMetadataResult>>([result]);
    }
}
