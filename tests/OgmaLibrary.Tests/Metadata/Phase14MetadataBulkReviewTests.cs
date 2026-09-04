using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Metadata;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Metadata;

/// <summary>Phase 14 atomic metadata batch-preview and undo tests.</summary>
public sealed class Phase14MetadataBulkReviewTests
{
    [Fact]
    public async Task PreviewApplyUndo_RestoresMetadataAndCatalogueColumns()
    {
        string dataDirectory = CreateTempDirectory();
        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            const string bookId = "01PH14BULK000000000000000001";
            await SeedBookAsync(provider, bookId);
            var review = provider.GetRequiredService<IMetadataReviewService>();
            IReadOnlyList<MetadataProposalDescriptor> proposals = await review.CreateAsync(bookId, [
                new MergedMetadataProposal("Title", "Curated title", "Original title", 0.91, "GoogleBooks", []),
                new MergedMetadataProposal("Year", "2024", "2019", 0.88, "OpenLibrary", []),
            ]);
            var bulk = provider.GetRequiredService<IMetadataBulkReviewService>();

            MetadataBulkReviewPreview preview = await bulk.PreviewAsync(
                proposals.Select(proposal => proposal.Id).ToArray());
            Assert.Equal(2, preview.Items.Count);
            Assert.Equal("Original title", preview.Items[0].BeforeValue);

            MetadataBulkReviewResult applied = await bulk.ApplyAsync(preview, "local-user");

            Assert.True(applied.IsAtomicSuccess);
            Assert.All(applied.Decisions, decision => Assert.True(decision.Applied));
            await using (CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>())
            {
                BookRow book = await context.Books.SingleAsync(row => row.BookId == bookId);
                Assert.Equal("Curated title", book.Title);
                Assert.Equal(2024, book.Year);
                Assert.Equal(2, await context.MetadataProposals.CountAsync(row =>
                    row.BookId == bookId && row.Status == (int)MetadataProposalStatus.Accepted));
            }

            Assert.True(await bulk.UndoAsync(applied.BatchId, applied.UndoToken, "local-user"));
            await using (CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>())
            {
                BookRow book = await context.Books.SingleAsync(row => row.BookId == bookId);
                Assert.Equal("Original title", book.Title);
                Assert.Equal(2019, book.Year);
                Assert.Equal("Original title", await context.BookMetadataFields
                    .Where(field => field.BookId == bookId && field.FieldName == "Title")
                    .Select(field => field.Value)
                    .SingleAsync());
            }

            Assert.False(await bulk.UndoAsync(applied.BatchId, applied.UndoToken, "local-user"));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task Apply_RejectsStalePreviewBeforeMutation()
    {
        string dataDirectory = CreateTempDirectory();
        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            const string bookId = "01PH14BULK000000000000000002";
            await SeedBookAsync(provider, bookId);
            var review = provider.GetRequiredService<IMetadataReviewService>();
            MetadataProposalDescriptor proposal = (await review.CreateAsync(bookId, [
                new MergedMetadataProposal("Title", "First title", "Original title", 0.9, "Provider", []),
            ])).Single();
            var bulk = provider.GetRequiredService<IMetadataBulkReviewService>();
            MetadataBulkReviewPreview preview = await bulk.PreviewAsync([proposal.Id]);
            await review.DecideAsync(proposal.Id, accept: false);

            await Assert.ThrowsAsync<InvalidOperationException>(() => bulk.ApplyAsync(preview, "local-user"));
            await using CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>();
            Assert.Equal("Original title", (await context.Books.SingleAsync(row => row.BookId == bookId)).Title);
            Assert.Equal("Original title", await context.BookMetadataFields
                .Where(field => field.BookId == bookId && field.FieldName == "Title")
                .Select(field => field.Value)
                .SingleAsync());
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task Undo_RefusesToOverwriteLaterMetadataEdit()
    {
        string dataDirectory = CreateTempDirectory();
        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            const string bookId = "01PH14BULK000000000000000003";
            await SeedBookAsync(provider, bookId);
            var review = provider.GetRequiredService<IMetadataReviewService>();
            MetadataProposalDescriptor proposal = (await review.CreateAsync(bookId, [
                new MergedMetadataProposal("Title", "Batch title", "Original title", 0.9, "Provider", []),
            ])).Single();
            var bulk = provider.GetRequiredService<IMetadataBulkReviewService>();
            MetadataBulkReviewPreview preview = await bulk.PreviewAsync([proposal.Id]);
            MetadataBulkReviewResult applied = await bulk.ApplyAsync(preview, "local-user");

            await using (CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>())
            {
                BookMetadataFieldRow field = await context.BookMetadataFields.SingleAsync(row =>
                    row.BookId == bookId && row.FieldName == "Title");
                field.Value = "Later manual edit";
                field.Source = "UserOverride";
                field.IsOverridden = true;
                await context.SaveChangesAsync();
            }

            await Assert.ThrowsAsync<InvalidOperationException>(() => bulk.UndoAsync(
                applied.BatchId,
                applied.UndoToken,
                "local-user"));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    private static async Task<ServiceProvider> CreateServicesAsync(string dataDirectory)
    {
        ServiceProvider provider = new ServiceCollection()
            .AddCatalogueContext(dataDirectory, dataDirectory)
            .AddMetadataEnrichment(dataDirectory)
            .BuildServiceProvider();
        await using CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>();
        await context.Database.MigrateAsync();
        return provider;
    }

    private static async Task SeedBookAsync(ServiceProvider provider, string bookId)
    {
        await using CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>();
        context.Books.Add(new BookRow
        {
            BookId = bookId,
            Title = "Original title",
            Year = 2019,
            Status = 0,
        });
        context.BookMetadataFields.Add(new BookMetadataFieldRow
        {
            BookId = bookId,
            FieldName = "Title",
            Value = "Original title",
            Source = "User",
            Confidence = 1.0,
            IsOverridden = false,
            SourceTimestamp = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ogma-phase14-bulk-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CleanupTempDirectory(string path)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
