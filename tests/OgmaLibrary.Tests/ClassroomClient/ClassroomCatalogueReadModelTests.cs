using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Infrastructure.ClassroomClient;

namespace OgmaLibrary.Tests.ClassroomClient;

/// <summary>Phase 17 Client-mode catalogue source integration tests.</summary>
public sealed class ClassroomCatalogueReadModelTests
{
    private const string Fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task ClassroomCatalogueReadModel_StandaloneMode_DelegatesToLocalCatalogue()
    {
        var local = new FakeLocalCatalogueReadModel();
        var mode = new InMemoryClassroomModeService();
        var connection = new InMemoryClassroomHostConnectionService();
        var host = new RecordingHostClient();
        var readModel = new ClassroomCatalogueReadModel(local, mode, connection, host);

        List<BookSummaryProjection> summaries = await CollectAsync(
            readModel.GetBookSummariesAsync(new CatalogueFilter(TitleContains: "Local")));
        BookDetailProjection? detail = await readModel.GetBookDetailAsync("local-book");
        List<ShelfProjection> shelves = await CollectAsync(readModel.GetShelvesAsync());
        ReadingProgressProjection? progress = await readModel.GetProgressAsync("local-book");

        Assert.Single(summaries);
        Assert.Equal("local-book", summaries[0].BookId);
        Assert.Equal("Local Title", detail!.Title);
        Assert.Single(shelves);
        Assert.Equal("local-book", progress!.BookId);
        Assert.Equal(1, local.SummaryCalls);
        Assert.Equal(1, local.DetailCalls);
        Assert.Equal(1, local.ShelfCalls);
        Assert.Equal(1, local.ProgressCalls);
        Assert.Equal(0, host.CatalogueCalls);
        Assert.Equal(0, host.BookCalls);
    }

    [Fact]
    public async Task ClassroomCatalogueReadModel_ClientModeWithoutConnection_ReturnsLocalFallback()
    {
        var local = new FakeLocalCatalogueReadModel();
        var mode = new InMemoryClassroomModeService();
        await mode.SaveModeAsync(new ClassroomModeSettings(LibraryRuntimeMode.ConnectToHost));
        var readModel = new ClassroomCatalogueReadModel(
            local,
            mode,
            new InMemoryClassroomHostConnectionService(),
            new RecordingHostClient());

        List<BookSummaryProjection> summaries = await CollectAsync(
            readModel.GetBookSummariesAsync(new CatalogueFilter()));

        Assert.Single(summaries);
        Assert.Equal("local-book", summaries[0].BookId);
        Assert.Equal(1, local.SummaryCalls);
    }

    [Fact]
    public async Task ClassroomCatalogueReadModel_ClientMode_MapsHostCataloguePages()
    {
        var local = new FakeLocalCatalogueReadModel();
        var mode = new InMemoryClassroomModeService();
        await mode.SaveModeAsync(new ClassroomModeSettings(LibraryRuntimeMode.ConnectToHost));
        var connection = new InMemoryClassroomHostConnectionService();
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);
        await connection.SetActiveAsync(new ClassroomHostConnection(
            request,
            "session-token",
            new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero)));
        var host = new RecordingHostClient
        {
            CataloguePages =
            [
                new LibraryHostCataloguePage(
                    [
                        new LibraryHostBookSummary(
                            "host-book-1",
                            "Host Title",
                            ["Host Author"],
                            Status: 0,
                            Rating: 5,
                            ShelfIds: ["shelf-science"],
                            ReadingProgressPct: 42.5,
                            IsAvailable: true,
                            Year: 2024,
                            ContentHash: "hash-1",
                            new LibraryHostAssetLinks("/covers/1.jpg", null, null)),
                    ],
                    Page: 1,
                    PageSize: 1,
                    ReturnedCount: 1,
                    HasMore: true),
                new LibraryHostCataloguePage(
                    [
                        new LibraryHostBookSummary(
                            "host-book-2",
                            "Second Host Title",
                            ["Second Author"],
                            Status: 0,
                            Rating: null,
                            ShelfIds: [],
                            ReadingProgressPct: null,
                            IsAvailable: false,
                            Year: null,
                            ContentHash: null,
                            new LibraryHostAssetLinks(null, null, null)),
                    ],
                    Page: 2,
                    PageSize: 1,
                    ReturnedCount: 1,
                    HasMore: false),
            ],
        };
        var readModel = new ClassroomCatalogueReadModel(local, mode, connection, host);

        List<BookSummaryProjection> summaries = await CollectAsync(
            readModel.GetBookSummariesAsync(new CatalogueFilter(
                TitleContains: "Host",
                AuthorContains: "Author",
                ShelfId: "shelf-science",
                Status: 0,
                MaxResults: 2)));

        Assert.Equal(2, summaries.Count);
        Assert.Equal("host-book-1", summaries[0].BookId);
        Assert.Equal("/covers/1.jpg", summaries[0].CoverRelativePath);
        Assert.Equal("hash-1", summaries[0].Sha256Hash);
        Assert.False(summaries[1].IsAvailable);
        Assert.Equal(2, host.CatalogueCalls);
        Assert.Equal("Host", host.LastQuery!.Title);
        Assert.Equal("Author", host.LastQuery.Author);
        Assert.Equal("shelf-science", host.LastQuery.ShelfId);
        Assert.Equal(0, host.LastQuery.Status);
        Assert.Equal(0, local.SummaryCalls);
    }

    [Fact]
    public async Task ClassroomCatalogueReadModel_ClientMode_MapsHostDetailAndProgress()
    {
        var local = new FakeLocalCatalogueReadModel();
        var mode = new InMemoryClassroomModeService();
        await mode.SaveModeAsync(new ClassroomModeSettings(LibraryRuntimeMode.ConnectToHost));
        var connection = new InMemoryClassroomHostConnectionService();
        await connection.SetActiveAsync(new ClassroomHostConnection(
            new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint),
            "session-token",
            new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero)));
        var host = new RecordingHostClient
        {
            Detail = new LibraryHostBookDetail(
                "host-book-1",
                "Host Detail",
                ["Host Author"],
                Year: 2024,
                Isbn: "9780000000002",
                Doi: "10.1000/ogma",
                Rating: 4,
                Status: 0,
                ContentHash: "hash-1",
                SizeBytes: 12345,
                ReadingProgress: new LibraryHostReadingProgress(
                    "host-book-1",
                    CurrentPage: 7,
                    CompletionPct: 70,
                    LastReadUtc: new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero),
                    Status: 1),
                Annotations: 3,
                MetadataFields:
                [
                    new LibraryHostMetadataField("Title", "Host Detail", "Host", 1.0, false),
                ],
                ReadingMemory: new LibraryHostReadingMemorySummary(
                    Disposition: 5,
                    KeyInsight: "Useful",
                    UpdatedAtUtc: new DateTimeOffset(2026, 6, 2, 12, 5, 0, TimeSpan.Zero)),
                IsOcrDerived: true,
                IsPasswordProtected: false,
                new LibraryHostAssetLinks("/covers/detail.jpg", null, null)),
        };
        var readModel = new ClassroomCatalogueReadModel(local, mode, connection, host);

        BookDetailProjection? detail = await readModel.GetBookDetailAsync("host-book-1");
        ReadingProgressProjection? progress = await readModel.GetProgressAsync("host-book-1");

        Assert.NotNull(detail);
        Assert.Equal("Host Detail", detail.Title);
        Assert.Equal("/covers/detail.jpg", detail.CoverRelativePath);
        Assert.Equal("host://host-book-1", detail.RelativePath);
        Assert.Equal("Useful", detail.ReadingMemory!.KeyInsight);
        Assert.True(detail.IsOcrDerived);
        Assert.Equal(7, progress!.CurrentPage);
        Assert.Equal(2, host.BookCalls);
        Assert.Equal(0, local.DetailCalls);
    }

    [Fact]
    public void CatalogueReadModel_IsModeAwareInCompositionRootShape()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<FakeLocalCatalogueReadModel>()
            .AddSingleton<IClassroomModeService, InMemoryClassroomModeService>()
            .AddSingleton<IClassroomHostConnectionService, InMemoryClassroomHostConnectionService>()
            .AddSingleton<ILibraryHostClient, RecordingHostClient>()
            .AddSingleton<ClassroomCatalogueReadModel>(sp => new ClassroomCatalogueReadModel(
                sp.GetRequiredService<FakeLocalCatalogueReadModel>(),
                sp.GetRequiredService<IClassroomModeService>(),
                sp.GetRequiredService<IClassroomHostConnectionService>(),
                sp.GetRequiredService<ILibraryHostClient>()))
            .AddSingleton<ICatalogueReadModel>(sp => sp.GetRequiredService<ClassroomCatalogueReadModel>())
            .BuildServiceProvider();

        ICatalogueReadModel readModel = provider.GetRequiredService<ICatalogueReadModel>();

        Assert.IsType<ClassroomCatalogueReadModel>(readModel);
    }

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var results = new List<T>();
        await foreach (T item in source)
        {
            results.Add(item);
        }

        return results;
    }

    private sealed class FakeLocalCatalogueReadModel : ICatalogueReadModel
    {
        public int SummaryCalls { get; private set; }

        public int DetailCalls { get; private set; }

        public int ShelfCalls { get; private set; }

        public int ProgressCalls { get; private set; }

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            SummaryCalls++;
            await Task.CompletedTask;
            yield return new BookSummaryProjection(
                "local-book",
                "Local Title",
                ["Local Author"],
                null,
                Status: 0,
                Rating: null,
                ShelfIds: [],
                ReadingProgressPct: 10,
                IsAvailable: true,
                Year: 2020,
                Sha256Hash: "local-hash");
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(
            string bookId,
            CancellationToken cancellationToken = default)
        {
            DetailCalls++;
            return Task.FromResult<BookDetailProjection?>(new BookDetailProjection(
                "local-book",
                "Local Title",
                ["Local Author"],
                Year: 2020,
                Isbn: null,
                Doi: null,
                Rating: null,
                Status: 0,
                CoverRelativePath: null,
                RelativePath: "local.pdf",
                Sha256Hash: "local-hash",
                SizeBytes: 100,
                ReadingProgress: null,
                Annotations: 0,
                MetadataFields: []));
        }

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ShelfCalls++;
            await Task.CompletedTask;
            yield return new ShelfProjection("local-shelf", "Local Shelf", IsSmart: false, BookCount: 1);
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(
            string bookId,
            CancellationToken cancellationToken = default)
        {
            ProgressCalls++;
            return Task.FromResult<ReadingProgressProjection?>(new ReadingProgressProjection(
                "local-book",
                CurrentPage: 1,
                CompletionPct: 10,
                LastReadUtc: null,
                Status: 1));
        }
    }

    private sealed class RecordingHostClient : ILibraryHostClient
    {
        public IReadOnlyList<LibraryHostCataloguePage> CataloguePages { get; init; } = [];

        public LibraryHostBookDetail Detail { get; init; } = new(
            "host-book-1",
            "Host Detail",
            [],
            Year: null,
            Isbn: null,
            Doi: null,
            Rating: null,
            Status: 0,
            ContentHash: null,
            SizeBytes: null,
            ReadingProgress: null,
            Annotations: 0,
            MetadataFields: [],
            ReadingMemory: null,
            IsOcrDerived: false,
            IsPasswordProtected: false,
            new LibraryHostAssetLinks(null, null, null));

        public int CatalogueCalls { get; private set; }

        public int BookCalls { get; private set; }

        public LibraryHostCatalogueQuery? LastQuery { get; private set; }

        public Task<LibraryHostHealth> GetHealthAsync(
            ClassroomJoinRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostSession> IssueSessionAsync(
            ClassroomJoinRequest request,
            Guid profileId,
            ClassroomRole role,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostCataloguePage> GetCataloguePageAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            LibraryHostCatalogueQuery query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            LibraryHostCataloguePage page = CataloguePages.Count >= query.Page
                ? CataloguePages[query.Page - 1]
                : new LibraryHostCataloguePage([], query.Page, query.PageSize, 0, HasMore: false);
            CatalogueCalls++;
            return Task.FromResult(page);
        }

        public Task<LibraryHostBookDetail> GetBookAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            string bookId,
            CancellationToken cancellationToken = default)
        {
            BookCalls++;
            return Task.FromResult(Detail);
        }

        public Task<LibraryHostSearchPage> SearchCatalogueAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            LibraryHostSearchQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostResource> GetPageRenderAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            string bookId,
            int pageNumber,
            int widthPx,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostResource> GetFileStreamAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            string bookId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostResource> GetAssetAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            string assetUrl,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UploadProfileSyncBlobAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            EncryptedClassroomSyncBlob blob,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EncryptedClassroomSyncBlob?> DownloadProfileSyncBlobAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
