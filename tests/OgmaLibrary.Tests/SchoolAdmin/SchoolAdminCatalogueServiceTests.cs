using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.SchoolAdmin;

namespace OgmaLibrary.Tests.SchoolAdmin;

/// <summary>Phase 18 data-backed School Administration catalogue tests.</summary>
public sealed class SchoolAdminCatalogueServiceTests
{
    [Fact]
    public async Task LibraryPublishingService_PublishAndUnpublish_RoundTripsPolicy()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            var publishing = provider.GetRequiredService<ILibraryPublishingService>();

            PublishedLibrary published = await publishing.PublishAsync(new PublishLibraryRequest(
                "main-library",
                "Main School Library",
                Path.Combine(dataDirectory, "books"),
                AiPrivacyTier.MetadataOnly));
            await publishing.UnpublishAsync("main-library");
            IReadOnlyList<PublishedLibrary> libraries = await publishing.ListAsync();

            Assert.True(published.IsPublished);
            Assert.Equal("main-library", published.LibraryId);
            Assert.Equal("Main School Library", published.DisplayName);
            Assert.Equal(AiPrivacyTier.MetadataOnly, published.AiTier);
            PublishedLibrary saved = Assert.Single(libraries);
            Assert.False(saved.IsPublished);
            Assert.Equal(published.SourcePath, saved.SourcePath);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task SharedShelfService_SaveListDelete_RoundTripsBooksAndGroups()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            await SeedBookAsync(provider, "01SCHOOLADMIN0000000001");
            await SeedBookAsync(provider, "01SCHOOLADMIN0000000002");
            var shelves = provider.GetRequiredService<ISharedShelfService>();

            SharedShelf saved = await shelves.SaveAsync(new SaveSharedShelfRequest(
                "reading-week",
                "Reading Week",
                SharedShelfVisibility.SpecificGroups,
                ["01SCHOOLADMIN0000000002", "01SCHOOLADMIN0000000001"],
                ["p7", "p6"]));
            IReadOnlyList<SharedShelf> listed = await shelves.ListAsync();
            await shelves.DeleteAsync("reading-week");
            IReadOnlyList<SharedShelf> afterDelete = await shelves.ListAsync();

            Assert.Equal("reading-week", saved.ShelfId);
            Assert.Equal(SharedShelfVisibility.SpecificGroups, saved.Visibility);
            Assert.Equal(["01SCHOOLADMIN0000000001", "01SCHOOLADMIN0000000002"], saved.BookIds);
            Assert.Equal(["p6", "p7"], saved.GroupIds);
            SharedShelf listedShelf = Assert.Single(listed);
            Assert.Equal(saved.BookIds, listedShelf.BookIds);
            Assert.Empty(afterDelete);
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
            .AddSchoolAdminServices(dataDirectory)
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
            Title = $"Book {bookId}",
            RelativePath = $"{bookId}.pdf",
            Sha256Hash = new string('a', 64),
            SizeBytes = 128,
            MtimeTicks = DateTimeOffset.UtcNow.UtcTicks,
            Status = 0,
            IndexStatus = 0,
            EmbeddingStatus = 0,
            IsOcrDerived = false,
            IsPasswordProtected = false,
        });
        await context.SaveChangesAsync();
    }

    private static string CreateTempDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-school-admin-catalogue-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDirectory);
        return dataDirectory;
    }

    private static void CleanupTempDirectory(string dataDirectory)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(dataDirectory))
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }
}
