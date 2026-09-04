using System.Net;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Assets;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Sidecar;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>Phase 16 tests for visual asset manifests and catalogue exposure.</summary>
public sealed class Phase16VisualAssetTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public Phase16VisualAssetTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task Manifest_CustomCoverWinsAndGeneratedReplacementCannotOverwriteIt()
    {
        const string bookId = "PHASE16-ASSET-BOOK";
        const string originalHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string replacementHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        _context.Books.Add(new BookRow { BookId = bookId, Title = "Manifest Book", Status = 0 });
        await _context.SaveChangesAsync();

        var service = new VisualAssetService(_context);
        await service.RegisterGeneratedAsync(
            bookId, originalHash, VisualAssetKind.Cover, "default", ".ogma/covers/aa/original.jpg",
            200, 300, "jpg", 1);
        await service.RegisterCustomCoverAsync(
            bookId, ".ogma/covers/custom/locked.png", 400, 600, "png");
        await service.RegisterGeneratedAsync(
            bookId, replacementHash, VisualAssetKind.Cover, "default", ".ogma/covers/bb/replacement.jpg",
            200, 300, "jpg", 2);
        await service.RegisterGeneratedAsync(
            bookId, originalHash, VisualAssetKind.Spine, "default", ".ogma/spines/aa/original.jpg",
            7, 100, "jpg", 1);

        VisualAssetDescriptor? preferred = await service.GetPreferredAsync(bookId, VisualAssetKind.Cover);
        Assert.NotNull(preferred);
        Assert.True(preferred.IsCustom);
        Assert.Equal(".ogma/covers/custom/locked.png", preferred.RelativePath);

        int invalidated = await service.InvalidateGeneratedAsync(bookId, replacementHash);
        Assert.Equal(1, invalidated);
        Assert.Equal(3, await _context.VisualAssetManifests.CountAsync());
    }

    [Fact]
    public async Task Manifest_ResolvesExactNamedVariantWithoutSizeFallback()
    {
        const string bookId = "PHASE16-VARIANT-BOOK";
        const string hash = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
        _context.Books.Add(new BookRow { BookId = bookId, Title = "Variant Book", Status = 0 });
        await _context.SaveChangesAsync();

        var service = new VisualAssetService(_context);
        await service.RegisterGeneratedAsync(
            bookId, hash, VisualAssetKind.Cover, "default", ".ogma/covers/cc/default.jpg",
            200, 300, "jpg", 1);
        await service.RegisterGeneratedAsync(
            bookId, hash, VisualAssetKind.Cover, "detail", ".ogma/covers/cc/detail.jpg",
            400, 600, "jpg", 1);

        VisualAssetDescriptor? detail = await service.GetVariantAsync(
            bookId, VisualAssetKind.Cover, "detail");
        Assert.NotNull(detail);
        Assert.Equal("detail", detail.Variant);
        Assert.Equal(400, detail.WidthPx);
        Assert.Equal(600, detail.HeightPx);

        Assert.Null(await service.GetVariantAsync(bookId, VisualAssetKind.Cover, "missing"));
    }

    [Fact]
    public void VariantCatalog_RejectsUnboundedOrWrongFamilyRequests()
    {
        Assert.Equal((200, 300),
            (VisualAssetVariants.CoverDefault.WidthPx, VisualAssetVariants.CoverDefault.HeightPx));
        Assert.Equal((14, 200),
            (VisualAssetVariants.Resolve(VisualAssetKind.Spine, "retina").WidthPx,
             VisualAssetVariants.Resolve(VisualAssetKind.Spine, "retina").HeightPx));
        Assert.Throws<ArgumentException>(() =>
            VisualAssetVariants.Resolve(VisualAssetKind.Cover, "retina"));
        Assert.Throws<ArgumentException>(() =>
            VisualAssetVariants.Resolve(VisualAssetKind.Spine, "detail"));
    }

    [Fact]
    public async Task ProviderCoverClient_AllowlistsEndpointAndValidatesDecodedImage()
    {
        byte[] png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        var handler = new StaticResponseHandler(png, "image/png");
        using var httpClient = new HttpClient(handler);
        var client = new ProviderCoverImageClient(httpClient);

        ProviderCoverImage image = await client.DownloadAsync(
            "https://covers.openlibrary.org/b/id/1-L.jpg");

        Assert.Equal(1, image.WidthPx);
        Assert.Equal(1, image.HeightPx);
        Assert.Equal("png", image.Format);
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(png)), image.Sha256);
        Assert.Equal(1, handler.RequestCount);

        await Assert.ThrowsAsync<ArgumentException>(() => client.DownloadAsync(
            "http://covers.openlibrary.org/b/id/1-L.jpg"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.DownloadAsync(
            "https://example.test/cover.png"));
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ProviderCoverAssetService_PersistsJpegAndRegistersProviderProvenance()
    {
        const string bookId = "PHASE16-PROVIDER-ASSET";
        const string hash = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
        _context.Books.Add(new BookRow { BookId = bookId, Title = "Provider Asset", Status = 0 });
        await _context.SaveChangesAsync();

        string root = Path.Combine(Path.GetTempPath(), $"ogma-provider-asset-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            byte[] png = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
            using var httpClient = new HttpClient(new StaticResponseHandler(png, "image/png"));
            var imageClient = new ProviderCoverImageClient(httpClient);
            var assets = new VisualAssetService(_context, root);
            var service = new ProviderCoverAssetService(
                new SidecarService(root),
                imageClient,
                assets);

            VisualAssetDescriptor descriptor = await service.PersistAsync(
                bookId,
                hash,
                "https://covers.openlibrary.org/b/id/2-L.jpg");

            Assert.Equal("provider", descriptor.Source);
            Assert.Equal("provider", descriptor.Variant);
            Assert.Equal("jpg", descriptor.Format);
            Assert.EndsWith("_provider.jpg", descriptor.RelativePath, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(root, descriptor.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
            Assert.Equal(descriptor, await assets.GetVariantAsync(bookId, VisualAssetKind.Cover, "provider"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CatalogueReadModel_ProjectsPreferredReadyCoverForSummaryAndDetail()
    {
        const string bookId = "PHASE16-READMODEL-BOOK";
        _context.Books.Add(new BookRow { BookId = bookId, Title = "Visible Cover", Status = 0 });
        _context.VisualAssetManifests.Add(new VisualAssetManifestRow
        {
            BookId = bookId,
            Kind = (int)VisualAssetKind.Cover,
            Variant = "default",
            RelativePath = ".ogma/covers/cc/visible.jpg",
            Source = "generated",
            WidthPx = 200,
            HeightPx = 300,
            Format = "jpg",
            GenerationVersion = 1,
            Status = (int)VisualAssetStatus.Ready,
            UpdatedUtc = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await _context.SaveChangesAsync();

        var readModel = new CatalogueReadModel(_context);
        var summaries = new List<BookSummaryProjection>();
        await foreach (BookSummaryProjection summary in readModel.GetBookSummariesAsync(new CatalogueFilter()))
        {
            summaries.Add(summary);
        }

        BookSummaryProjection projectedSummary = Assert.Single(summaries);
        BookDetailProjection? detail = await readModel.GetBookDetailAsync(bookId);

        Assert.Equal(".ogma/covers/cc/visible.jpg", projectedSummary.CoverRelativePath);
        Assert.NotNull(detail);
        Assert.Equal(".ogma/covers/cc/visible.jpg", detail.CoverRelativePath);
    }

    [Fact]
    public async Task Manifest_RejectsAbsoluteAndTraversalPaths()
    {
        const string bookId = "PHASE16-SAFE-PATH-BOOK";
        _context.Books.Add(new BookRow { BookId = bookId, Status = 0 });
        await _context.SaveChangesAsync();
        var service = new VisualAssetService(_context);

        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterCustomCoverAsync(
            bookId, "C:/outside.png", 200, 300, "png"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterCustomCoverAsync(
            bookId, ".ogma/covers/../outside.png", 200, 300, "png"));
    }

    [Fact]
    public async Task GarbageCollection_RemovesStaleUnreferencedFiles_AndRetainsSharedFiles()
    {
        const string staleBook = "PHASE16-GC-STALE";
        const string sharedBook = "PHASE16-GC-SHARED";
        string root = Path.Combine(Path.GetTempPath(), $"ogma-asset-gc-{Guid.NewGuid():N}");
        string staleRelativePath = ".ogma/covers/gc/stale.jpg";
        string sharedRelativePath = ".ogma/covers/gc/shared.jpg";
        Directory.CreateDirectory(Path.Combine(root, ".ogma", "covers", "gc"));
        await File.WriteAllBytesAsync(Path.Combine(root, staleRelativePath.Replace('/', Path.DirectorySeparatorChar)), [1]);
        await File.WriteAllBytesAsync(Path.Combine(root, sharedRelativePath.Replace('/', Path.DirectorySeparatorChar)), [2]);

        try
        {
            _context.Books.AddRange(
                new BookRow { BookId = staleBook, Title = "Stale", Status = 0 },
                new BookRow { BookId = sharedBook, Title = "Shared", Status = 0 });
            _context.VisualAssetManifests.AddRange(
                new VisualAssetManifestRow
                {
                    BookId = staleBook,
                    Kind = (int)VisualAssetKind.Cover,
                    Variant = "stale",
                    RelativePath = staleRelativePath,
                    Source = "generated",
                    WidthPx = 100,
                    HeightPx = 100,
                    Format = "jpg",
                    GenerationVersion = 1,
                    Status = (int)VisualAssetStatus.Stale,
                    UpdatedUtc = DateTimeOffset.UtcNow,
                    CreatedUtc = DateTimeOffset.UtcNow,
                },
                new VisualAssetManifestRow
                {
                    BookId = staleBook,
                    Kind = (int)VisualAssetKind.Cover,
                    Variant = "shared-stale",
                    RelativePath = sharedRelativePath,
                    Source = "generated",
                    WidthPx = 100,
                    HeightPx = 100,
                    Format = "jpg",
                    GenerationVersion = 1,
                    Status = (int)VisualAssetStatus.Stale,
                    UpdatedUtc = DateTimeOffset.UtcNow,
                    CreatedUtc = DateTimeOffset.UtcNow,
                },
                new VisualAssetManifestRow
                {
                    BookId = sharedBook,
                    Kind = (int)VisualAssetKind.Cover,
                    Variant = "ready",
                    RelativePath = sharedRelativePath,
                    Source = "generated",
                    WidthPx = 100,
                    HeightPx = 100,
                    Format = "jpg",
                    GenerationVersion = 1,
                    Status = (int)VisualAssetStatus.Ready,
                    UpdatedUtc = DateTimeOffset.UtcNow,
                    CreatedUtc = DateTimeOffset.UtcNow,
                });
            await _context.SaveChangesAsync();

            var service = new VisualAssetService(_context, root);
            VisualAssetGarbageCollectionResult result = await service.CollectStaleAsync(staleBook);

            Assert.Equal(2, result.RemovedManifestEntries);
            Assert.Equal(1, result.DeletedFiles);
            Assert.Equal(1, result.RetainedReferencedFiles);
            Assert.False(File.Exists(Path.Combine(root, staleRelativePath.Replace('/', Path.DirectorySeparatorChar))));
            Assert.True(File.Exists(Path.Combine(root, sharedRelativePath.Replace('/', Path.DirectorySeparatorChar))));
            Assert.Single(_context.VisualAssetManifests.Where(asset => asset.BookId == sharedBook));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }

    private sealed class StaticResponseHandler(byte[] bytes, string mediaType) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes),
                RequestMessage = request,
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
            return Task.FromResult(response);
        }
    }
}
