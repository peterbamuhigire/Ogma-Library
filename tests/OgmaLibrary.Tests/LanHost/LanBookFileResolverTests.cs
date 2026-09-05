using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.LanHost;
using OgmaLibrary.Infrastructure.Pathing;

namespace OgmaLibrary.Tests.LanHost;

/// <summary>Phase 16 FileStream resolver safety tests.</summary>
public sealed class LanBookFileResolverTests
{
    [Fact]
    public async Task ResolveAsync_ReturnsPresentPdfWithinLibraryRoot()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider services = await CreateServicesAsync(dataDirectory);
            await SeedBookFileAsync(services, "01LANRESOLVER00000000001", "books/present.pdf", fileStatus: 0);
            Directory.CreateDirectory(Path.Combine(dataDirectory, "books"));
            await File.WriteAllTextAsync(Path.Combine(dataDirectory, "books", "present.pdf"), "%PDF-1.7");

            string? resolved = await services.GetRequiredService<ILanBookFileResolver>()
                .ResolveAsync("01LANRESOLVER00000000001", CancellationToken.None);

            Assert.Equal(
                PathGuard.CanonicalizeRoot(Path.Combine(dataDirectory, "books", "present.pdf")),
                resolved);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task ResolveAsync_RejectsTraversalOutsideLibraryRoot()
    {
        string dataDirectory = CreateTempDirectory();
        string outsidePath = Path.Combine(Path.GetTempPath(), $"ogma-lanhost-outside-{Guid.NewGuid():N}.pdf");

        try
        {
            await using ServiceProvider services = await CreateServicesAsync(dataDirectory);
            await SeedBookFileAsync(services, "01LANRESOLVER00000000002", "../" + Path.GetFileName(outsidePath), fileStatus: 0);
            await File.WriteAllTextAsync(outsidePath, "%PDF-1.7");

            string? resolved = await services.GetRequiredService<ILanBookFileResolver>()
                .ResolveAsync("01LANRESOLVER00000000002", CancellationToken.None);

            Assert.Null(resolved);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
            if (File.Exists(outsidePath))
            {
                File.Delete(outsidePath);
            }
        }
    }

    [Fact]
    public async Task ResolveAsync_RejectsRootedCataloguePath()
    {
        string dataDirectory = CreateTempDirectory();
        string rootedOutsidePath = Path.Combine(Path.GetTempPath(), $"ogma-lanhost-rooted-{Guid.NewGuid():N}.pdf");

        try
        {
            await using ServiceProvider services = await CreateServicesAsync(dataDirectory);
            await SeedBookFileAsync(
                services,
                "01LANRESOLVER00000000003",
                rootedOutsidePath.Replace(Path.DirectorySeparatorChar, '/'),
                fileStatus: 0);
            await File.WriteAllTextAsync(rootedOutsidePath, "%PDF-1.7");

            string? resolved = await services.GetRequiredService<ILanBookFileResolver>()
                .ResolveAsync("01LANRESOLVER00000000003", CancellationToken.None);

            Assert.Null(resolved);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
            if (File.Exists(rootedOutsidePath))
            {
                File.Delete(rootedOutsidePath);
            }
        }
    }

    [Fact]
    public async Task ResolveAsync_IgnoresMissingOrUnavailableFiles()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider services = await CreateServicesAsync(dataDirectory);
            await SeedBookFileAsync(services, "01LANRESOLVER00000000004", "books/missing.pdf", fileStatus: 0);
            await SeedBookFileAsync(services, "01LANRESOLVER00000000005", "books/unavailable.pdf", fileStatus: 1);
            Directory.CreateDirectory(Path.Combine(dataDirectory, "books"));
            await File.WriteAllTextAsync(Path.Combine(dataDirectory, "books", "unavailable.pdf"), "%PDF-1.7");

            ILanBookFileResolver resolver = services.GetRequiredService<ILanBookFileResolver>();

            Assert.Null(await resolver.ResolveAsync("01LANRESOLVER00000000004", CancellationToken.None));
            Assert.Null(await resolver.ResolveAsync("01LANRESOLVER00000000005", CancellationToken.None));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    private static async Task<ServiceProvider> CreateServicesAsync(string dataDirectory)
    {
        ServiceProvider services = new ServiceCollection()
            .AddCatalogueContext(dataDirectory, dataDirectory)
            .AddLanHostServices(dataDirectory)
            .BuildServiceProvider();

        await using CatalogueDbContext context = services.GetRequiredService<CatalogueDbContext>();
        await context.Database.MigrateAsync();
        return services;
    }

    private static async Task SeedBookFileAsync(
        ServiceProvider services,
        string bookId,
        string relativePath,
        int fileStatus)
    {
        await using CatalogueDbContext context = services.GetRequiredService<CatalogueDbContext>();
        context.Books.Add(new BookRow
        {
            BookId = bookId,
            Title = "LAN Resolver Book",
            RelativePath = relativePath,
            Sha256Hash = new string('f', 64),
            SizeBytes = 128,
            MtimeTicks = DateTimeOffset.UtcNow.UtcTicks,
            Status = 0,
            IndexStatus = 0,
            EmbeddingStatus = 0,
            IsOcrDerived = false,
            IsPasswordProtected = false,
            Year = 2026,
            BookFiles =
            [
                new BookFileRow
                {
                    RelativePath = relativePath,
                    FileStatus = fileStatus,
                    LastSeenUtc = DateTimeOffset.UtcNow,
                },
            ],
        });
        await context.SaveChangesAsync();
    }

    private static string CreateTempDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-lanhost-resolver-{Guid.NewGuid():N}");
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
