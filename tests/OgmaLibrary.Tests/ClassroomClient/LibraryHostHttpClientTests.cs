using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Infrastructure.ClassroomClient;

namespace OgmaLibrary.Tests.ClassroomClient;

/// <summary>Phase 17 Host API client tests.</summary>
public sealed class LibraryHostHttpClientTests
{
    private const string Fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly string[] AdaAuthor = ["Ada"];
    private static readonly string[] MathShelf = ["math"];
    private static readonly string[] TitleMatchedFields = ["Title"];

    [Fact]
    public async Task LibraryHostHttpClient_MapsHealthResponse()
    {
        var handler = new QueueHttpHandler();
        handler.EnqueueJson(new
        {
            state = "running",
            port = 7473,
            bindAddress = "192.168.1.13",
            certificateFingerprint = Fingerprint,
            requiresAuth = true,
        });
        using var client = new LibraryHostHttpClient(new HttpClient(handler));
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint, DisplayName: "School Library");

        LibraryHostHealth health = await client.GetHealthAsync(request);

        Assert.Equal("School Library", health.DisplayName);
        Assert.Equal(Fingerprint, health.CertificateFingerprint);
        Assert.Equal("unknown", health.ContentMode);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal("https://192.168.1.13:7473/api/v1/health", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task LibraryHostHttpClient_IssuesSessionWithEnrollmentCode()
    {
        var handler = new QueueHttpHandler();
        DateTimeOffset expires = new(2026, 6, 2, 12, 30, 0, TimeSpan.Zero);
        handler.EnqueueJson(new { token = "session-token", expiresUtc = expires });
        using var client = new LibraryHostHttpClient(new HttpClient(handler));
        var request = new ClassroomJoinRequest(
            "192.168.1.13",
            7473,
            Fingerprint,
            EnrollmentCode: "123456");
        Guid profileId = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");

        LibraryHostSession session = await client.IssueSessionAsync(
            request,
            profileId,
            ClassroomRole.Student,
            TimeSpan.FromMinutes(30));

        string body = await handler.Requests[0].Content!.ReadAsStringAsync();
        using JsonDocument payload = JsonDocument.Parse(body);
        Assert.Equal("session-token", session.Token);
        Assert.Equal(expires, session.ExpiresUtc);
        Assert.Equal("https://192.168.1.13:7473/api/v1/auth/session", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal(profileId.ToString("N"), payload.RootElement.GetProperty("clientId").GetString());
        Assert.Equal("student", payload.RootElement.GetProperty("role").GetString());
        Assert.Equal(30, payload.RootElement.GetProperty("lifetimeMinutes").GetInt32());
        Assert.Equal("123456", payload.RootElement.GetProperty("enrollmentCode").GetString());
    }

    [Fact]
    public async Task LibraryHostHttpClient_GetsCataloguePageWithBearerToken()
    {
        var handler = new QueueHttpHandler();
        handler.EnqueueJson(new
        {
            items = new[]
            {
                new
                {
                    bookId = "book-1",
                    title = "Algebra I",
                    authors = AdaAuthor,
                    status = 1,
                    rating = 5,
                    shelfIds = MathShelf,
                    readingProgressPct = 25.0,
                    isAvailable = true,
                    year = 2026,
                    contentHash = Fingerprint,
                    assets = new
                    {
                        coverUrl = "/api/v1/assets/cover/hash",
                        spineUrl = (string?)null,
                        thumbnailUrl = "/api/v1/assets/thumb/hash",
                    },
                },
            },
            page = 2,
            pageSize = 10,
            returnedCount = 1,
            hasMore = false,
        });
        using var client = new LibraryHostHttpClient(new HttpClient(handler));
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);

        LibraryHostCataloguePage page = await client.GetCataloguePageAsync(
            request,
            "session-token",
            new LibraryHostCatalogueQuery(Title: "algebra", Author: "Ada Lovelace", Page: 2, PageSize: 10));

        LibraryHostBookSummary book = Assert.Single(page.Items);
        Assert.Equal(2, page.Page);
        Assert.Equal("book-1", book.BookId);
        Assert.Equal("Algebra I", book.Title);
        Assert.Equal("/api/v1/assets/cover/hash", book.Assets.CoverUrl);
        Assert.Equal("Bearer", handler.Requests[0].Headers.Authorization!.Scheme);
        Assert.Equal("session-token", handler.Requests[0].Headers.Authorization!.Parameter);
        Assert.Equal(
            "https://192.168.1.13:7473/api/v1/catalogue?page=2&pageSize=10&title=algebra&author=Ada%20Lovelace",
            handler.Requests[0].RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task LibraryHostHttpClient_GetsBookDetailWithBearerToken()
    {
        var handler = new QueueHttpHandler();
        DateTimeOffset lastRead = new(2026, 6, 2, 11, 45, 0, TimeSpan.Zero);
        handler.EnqueueJson(new
        {
            bookId = "book 1",
            title = "Algebra I",
            authors = AdaAuthor,
            year = 2026,
            isbn = "9780000000001",
            doi = "10.1000/ogma",
            rating = 5,
            status = 1,
            contentHash = Fingerprint,
            sizeBytes = 12345,
            readingProgress = new
            {
                bookId = "book 1",
                currentPage = 4,
                completionPct = 25.0,
                lastReadUtc = lastRead,
                status = 1,
            },
            annotations = 3,
            metadataFields = new[]
            {
                new
                {
                    fieldName = "Title",
                    value = "Algebra I",
                    source = "manual",
                    confidence = 1.0,
                    isOverridden = true,
                },
            },
            readingMemory = new
            {
                disposition = 5,
                keyInsight = "Linear equations need practice.",
                updatedAtUtc = lastRead,
            },
            isOcrDerived = false,
            isPasswordProtected = false,
            assets = new
            {
                coverUrl = "/api/v1/assets/cover/hash",
                spineUrl = (string?)null,
                thumbnailUrl = "/api/v1/assets/thumb/hash",
            },
        });
        using var client = new LibraryHostHttpClient(new HttpClient(handler));
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);

        LibraryHostBookDetail detail = await client.GetBookAsync(request, "session-token", "book 1");

        Assert.Equal("book 1", detail.BookId);
        Assert.Equal("Algebra I", detail.Title);
        Assert.Equal(4, detail.ReadingProgress!.CurrentPage);
        Assert.Equal("Title", Assert.Single(detail.MetadataFields).FieldName);
        Assert.Equal("Linear equations need practice.", detail.ReadingMemory!.KeyInsight);
        Assert.Equal("/api/v1/assets/thumb/hash", detail.Assets.ThumbnailUrl);
        Assert.Equal("session-token", handler.Requests[0].Headers.Authorization!.Parameter);
        Assert.Equal(
            "https://192.168.1.13:7473/api/v1/catalogue/book%201",
            handler.Requests[0].RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task LibraryHostHttpClient_SearchesCatalogueWithBearerToken()
    {
        var handler = new QueueHttpHandler();
        handler.EnqueueJson(new
        {
            query = "algebra",
            items = new[]
            {
                new
                {
                    bookId = "book-1",
                    title = "Algebra I",
                    author = "Ada",
                    score = 100,
                    matchedFields = TitleMatchedFields,
                },
            },
            returnedCount = 1,
            hasMore = false,
        });
        using var client = new LibraryHostHttpClient(new HttpClient(handler));
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);

        LibraryHostSearchPage results = await client.SearchCatalogueAsync(
            request,
            "session-token",
            new LibraryHostSearchQuery("algebra", PageSize: 12));

        LibraryHostSearchResult result = Assert.Single(results.Items);
        Assert.Equal("algebra", results.Query);
        Assert.Equal("book-1", result.BookId);
        Assert.Equal("Title", Assert.Single(result.MatchedFields));
        Assert.Equal("session-token", handler.Requests[0].Headers.Authorization!.Parameter);
        Assert.Equal(
            "https://192.168.1.13:7473/api/v1/catalogue/search?pageSize=12&q=algebra",
            handler.Requests[0].RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task LibraryHostHttpClient_GetsPageRenderResource()
    {
        var handler = new QueueHttpHandler();
        handler.EnqueueBytes([137, 80, 78, 71], "image/png", "\"page-etag\"");
        using var client = new LibraryHostHttpClient(new HttpClient(handler));
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);

        LibraryHostResource resource = await client.GetPageRenderAsync(
            request,
            "session-token",
            "book 1",
            pageNumber: 3,
            widthPx: 1200);

        Assert.Equal("books/book%201/page/3?widthPx=1200", resource.ResourceKey);
        Assert.Equal("image/png", resource.ContentType);
        Assert.Equal("\"page-etag\"", resource.ETag);
        Assert.Equal([137, 80, 78, 71], resource.Content);
        Assert.Equal(
            "https://192.168.1.13:7473/api/v1/books/book%201/page/3?widthPx=1200",
            handler.Requests[0].RequestUri!.AbsoluteUri);
        Assert.Equal("session-token", handler.Requests[0].Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task LibraryHostHttpClient_GetsFileStreamResource()
    {
        var handler = new QueueHttpHandler();
        handler.EnqueueBytes([37, 80, 68, 70], "application/pdf", null);
        using var client = new LibraryHostHttpClient(new HttpClient(handler));
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);

        LibraryHostResource resource = await client.GetFileStreamAsync(request, "session-token", "book-1");

        Assert.Equal("books/book-1/file", resource.ResourceKey);
        Assert.Equal("application/pdf", resource.ContentType);
        Assert.Equal([37, 80, 68, 70], resource.Content);
        Assert.Equal(
            "https://192.168.1.13:7473/api/v1/books/book-1/file",
            handler.Requests[0].RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task LibraryHostHttpClient_GetsAssetResourceFromProjectedUrl()
    {
        var handler = new QueueHttpHandler();
        handler.EnqueueBytes([255, 216], "image/jpeg", "\"asset-etag\"");
        using var client = new LibraryHostHttpClient(new HttpClient(handler));
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);

        LibraryHostResource resource = await client.GetAssetAsync(
            request,
            "session-token",
            "/api/v1/assets/cover/0123456789abcdef");

        Assert.Equal("api/v1/assets/cover/0123456789abcdef", resource.ResourceKey);
        Assert.Equal("image/jpeg", resource.ContentType);
        Assert.Equal("\"asset-etag\"", resource.ETag);
        Assert.Equal([255, 216], resource.Content);
    }

    [Fact]
    public async Task LibraryHostHttpClient_UploadsAndDownloadsEncryptedProfileSyncBlob()
    {
        var handler = new QueueHttpHandler();
        handler.EnqueueStatus(HttpStatusCode.NoContent);
        handler.EnqueueBytes([1, 2, 3, 4], "application/vnd.ogma.classroom-sync+bin", null, version: 7);
        using var client = new LibraryHostHttpClient(new HttpClient(handler));
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);
        var blob = new EncryptedClassroomSyncBlob(
            Version: 7,
            ContentType: "application/vnd.ogma.classroom-sync+bin",
            Content: [1, 2, 3, 4]);

        await client.UploadProfileSyncBlobAsync(request, "session-token", blob);
        EncryptedClassroomSyncBlob? downloaded = await client.DownloadProfileSyncBlobAsync(request, "session-token");

        Assert.NotNull(downloaded);
        Assert.Equal(7, downloaded.Version);
        Assert.Equal("application/vnd.ogma.classroom-sync+bin", downloaded.ContentType);
        Assert.Equal([1, 2, 3, 4], downloaded.Content);
        Assert.Equal(HttpMethod.Put, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
        Assert.Equal("session-token", handler.Requests[0].Headers.Authorization!.Parameter);
        Assert.Equal("7", handler.Requests[0].Headers.GetValues("X-Ogma-Sync-Version").Single());
        Assert.Equal(
            "https://192.168.1.13:7473/api/v1/profile/sync",
            handler.Requests[0].RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task LibraryHostHttpClient_RejectsNonHostAssetUrl()
    {
        using var client = new LibraryHostHttpClient(new HttpClient(new QueueHttpHandler()));
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetAssetAsync(
            request,
            "session-token",
            "https://example.com/cover.jpg"));
    }

    private sealed class QueueHttpHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public List<HttpRequestMessage> Requests { get; } = [];

        public void EnqueueJson(object payload)
        {
            string json = JsonSerializer.Serialize(payload);
            _responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            });
        }

        public void EnqueueStatus(HttpStatusCode statusCode)
        {
            _responses.Enqueue(new HttpResponseMessage(statusCode));
        }

        public void EnqueueBytes(byte[] content, string contentType, string? eTag, int? version = null)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            if (eTag is not null)
            {
                response.Headers.ETag = new EntityTagHeaderValue(eTag);
            }

            if (version is not null)
            {
                response.Headers.TryAddWithoutValidation(
                    "X-Ogma-Sync-Version",
                    version.Value.ToString(CultureInfo.InvariantCulture));
            }

            _responses.Enqueue(response);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
