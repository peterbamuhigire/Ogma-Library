using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Tests.Search;

/// <summary>Phase 22 page, facet, highlight, and full-text fallback tests.</summary>
public sealed class Phase22CatalogueSearchQueryTests
{
    [Fact]
    public async Task Search_ReturnsStablePagesFacetsAndSafeHighlights()
    {
        string dataDirectory = CreateTempDirectory();
        try
        {
            await using ServiceProvider provider = await CreateProviderAsync(dataDirectory);
            await using CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>();
            for (int index = 0; index < 23; index++)
            {
                context.Books.Add(new BookRow
                {
                    BookId = $"PH22PAGE{index:D20}",
                    Title = $"Atlas {index:D2}",
                    Status = 0,
                });
            }
            await context.SaveChangesAsync();

            ICatalogueSearchService search = provider.GetRequiredService<ICatalogueSearchService>();
            CatalogueSearchPage first = await search.SearchAsync(new("Atlas", PageSize: 10));
            CatalogueSearchPage second = await search.SearchAsync(new("Atlas", Page: 2, PageSize: 10));

            Assert.Equal(23, first.TotalCount);
            Assert.Equal(10, first.Items.Count);
            Assert.Equal(10, second.Items.Count);
            Assert.Empty(first.Items.Select(item => item.BookId).Intersect(second.Items.Select(item => item.BookId)));
            Assert.Equal("PH22PAGE00000000000000000000", first.Items[0].BookId);
            Assert.Equal("Atlas 00", first.Items[0].HighlightedTitle?.Text);
            Assert.Equal([new SearchSnippetSpan(0, 5)], first.Items[0].HighlightedTitle?.Spans);
            Assert.Contains(first.Facets, facet => facet.Field == "title" && facet.Count == 10);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task Search_UsesFullTextFallbackWhenMetadataHasNoMatch()
    {
        string dataDirectory = CreateTempDirectory();
        try
        {
            await using ServiceProvider provider = await CreateProviderAsync(dataDirectory);
            await using CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>();
            const string bookId = "PH22FTSFALLBACK00000000000001";
            context.Books.Add(new BookRow { BookId = bookId, Title = "Indexed title", Status = 0 });
            await context.SaveChangesAsync();
            context.SearchChunks.Add(new SearchChunkRow
            {
                BookId = bookId,
                ChunkText = "rare fallback passage",
                ChunkIndex = 0,
                Source = (int)SearchChunkSource.Page,
                TokenCount = 3,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();

            CatalogueSearchPage result = await provider.GetRequiredService<ICatalogueSearchService>()
                .SearchAsync(new("fallback"));

            Assert.True(result.UsedFullTextFallback);
            Assert.Contains(result.Items, item => item.BookId == bookId);
            Assert.NotEmpty(result.Items[0].FullTextHits!);
            Assert.Contains("full-text:page", result.Items[0].MatchedFields);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task Search_RejectsInvalidPageSizeAndEscapesLiteralWildcards()
    {
        string dataDirectory = CreateTempDirectory();
        try
        {
            await using ServiceProvider provider = await CreateProviderAsync(dataDirectory);
            await using CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>();
            context.Books.Add(new BookRow { BookId = "PH22LITERAL00000000000000001", Title = "100% safe", Status = 0 });
            await context.SaveChangesAsync();
            ICatalogueSearchService search = provider.GetRequiredService<ICatalogueSearchService>();

            CatalogueSearchPage result = await search.SearchAsync(new("100%"));

            Assert.Single(result.Items);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                search.SearchAsync(new("safe", PageSize: 101)));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    private static async Task<ServiceProvider> CreateProviderAsync(string dataDirectory)
    {
        ServiceProvider provider = new ServiceCollection()
            .AddCatalogueContext(dataDirectory, dataDirectory)
            .BuildServiceProvider();
        await using CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>();
        await context.Database.MigrateAsync();
        return provider;
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ogma-phase22-search-{Guid.NewGuid():N}");
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
