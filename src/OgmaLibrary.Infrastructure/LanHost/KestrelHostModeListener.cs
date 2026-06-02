using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Domain;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Loopback HTTPS listener for the first Phase 16 Host-mode endpoints.</summary>
internal sealed class KestrelHostModeListener : IHostModeListener
{
    private const string IssuedSessionItemKey = "Ogma.LanHost.IssuedSession";
    private const string AuthenticatedSessionItemKey = "Ogma.LanHost.AuthenticatedSession";
    private const int MaxProfileSyncBlobBytes = 5 * 1024 * 1024;

    private readonly ICatalogueReadModel _catalogueReadModel;
    private readonly IMetadataSearchService _metadataSearch;
    private readonly ISidecarService _sidecarService;
    private readonly ILanBookFileResolver _fileResolver;
    private readonly ILanPageRenderer _pageRenderer;
    private readonly ILanPageRenderLimiter _pageRenderLimiter;
    private readonly IClientSessionService _sessions;
    private readonly IProfileSyncBlobStore _profileSyncBlobs;
    private readonly IHostServerCertificateProvider _certificates;
    private readonly IAuditRepository _audit;
    private readonly ILanBindAddressSelector _bindAddressSelector;
    private readonly ILanClientAddressPolicy _clientAddressPolicy;
    private WebApplication? _app;

    public KestrelHostModeListener(
        ICatalogueReadModel catalogueReadModel,
        IMetadataSearchService metadataSearch,
        ISidecarService sidecarService,
        ILanBookFileResolver fileResolver,
        ILanPageRenderer pageRenderer,
        ILanPageRenderLimiter pageRenderLimiter,
        IClientSessionService sessions,
        IProfileSyncBlobStore profileSyncBlobs,
        IHostServerCertificateProvider certificates,
        IAuditRepository audit,
        ILanBindAddressSelector bindAddressSelector,
        ILanClientAddressPolicy clientAddressPolicy)
    {
        _catalogueReadModel = catalogueReadModel ?? throw new ArgumentNullException(nameof(catalogueReadModel));
        _metadataSearch = metadataSearch ?? throw new ArgumentNullException(nameof(metadataSearch));
        _sidecarService = sidecarService ?? throw new ArgumentNullException(nameof(sidecarService));
        _fileResolver = fileResolver ?? throw new ArgumentNullException(nameof(fileResolver));
        _pageRenderer = pageRenderer ?? throw new ArgumentNullException(nameof(pageRenderer));
        _pageRenderLimiter = pageRenderLimiter ?? throw new ArgumentNullException(nameof(pageRenderLimiter));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _profileSyncBlobs = profileSyncBlobs ?? throw new ArgumentNullException(nameof(profileSyncBlobs));
        _certificates = certificates ?? throw new ArgumentNullException(nameof(certificates));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _bindAddressSelector = bindAddressSelector ?? throw new ArgumentNullException(nameof(bindAddressSelector));
        _clientAddressPolicy = clientAddressPolicy ?? throw new ArgumentNullException(nameof(clientAddressPolicy));
    }

    /// <inheritdoc />
    public async Task StartAsync(
        HostModeSettings settings,
        string certificateFingerprint,
        string enrollmentCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(enrollmentCode);
        await StopAsync(cancellationToken).ConfigureAwait(false);
        IPAddress bindAddress = _bindAddressSelector.SelectBindAddress();

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(
                bindAddress,
                settings.Port,
                listen => listen.UseHttps(_certificates.LoadOrCreateCertificateAsync(bindAddress, cancellationToken)
                    .GetAwaiter()
                    .GetResult()));
        });
        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.AddServerHeader = false;
        });

        WebApplication app = builder.Build();
        ConfigurePipeline(app, settings, certificateFingerprint, enrollmentCode, bindAddress);

        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        _app = app;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_app is null)
        {
            return;
        }

        await _app.StopAsync(cancellationToken).ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
        _app = null;
    }

    private void ConfigurePipeline(
        WebApplication app,
        HostModeSettings settings,
        string certificateFingerprint,
        string enrollmentCode,
        IPAddress bindAddress)
    {
        app.Use(async (context, next) =>
        {
            long started = Environment.TickCount64;
            string? token = ReadBearerToken(context.Request);
            ClientSessionSnapshot? session = null;
            bool authenticated = false;

            if (!_clientAddressPolicy.IsAllowed(context.Connection.RemoteIpAddress))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    new LanHostError("client_address_forbidden", "LAN Host only accepts loopback fallback or private LAN clients."),
                    context.RequestAborted).ConfigureAwait(false);
                await AppendAuditAsync(context, token, session, authenticated, settings.ContentMode, started).ConfigureAwait(false);
                return;
            }

            if (IsPublicEndpoint(context.Request.Path))
            {
                await next(context).ConfigureAwait(false);
                session = GetIssuedSession(context);
                await AppendAuditAsync(context, token, session, authenticated, settings.ContentMode, started).ConfigureAwait(false);
                return;
            }

            session = token is null
                ? null
                : await _sessions.GetActiveAsync(token, context.RequestAborted).ConfigureAwait(false);
            if (session is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new LanHostError("unauthorized", "A valid LAN Host session token is required."),
                    context.RequestAborted).ConfigureAwait(false);
                await AppendAuditAsync(context, token, session, authenticated, settings.ContentMode, started).ConfigureAwait(false);
                return;
            }

            authenticated = true;
            context.Items[AuthenticatedSessionItemKey] = session;
            await next(context).ConfigureAwait(false);
            await AppendAuditAsync(context, token, session, authenticated, settings.ContentMode, started).ConfigureAwait(false);
        });

        app.MapGet("/api/v1/health", () => Results.Json(new LanHostHealthResponse(
            State: "running",
            Port: settings.Port,
            BindAddress: bindAddress.ToString(),
            CertificateFingerprint: certificateFingerprint,
            RequiresAuth: true)));

        app.MapPost("/api/v1/auth/session", async (LanSessionIssueRequest request, HttpContext context, CancellationToken ct) =>
        {
            if (!IsEnrollmentCodeValid(request.EnrollmentCode, enrollmentCode))
            {
                return Results.Json(
                    new LanHostError(
                        "invalid_enrollment_code",
                        "A valid LAN Host enrollment code is required to create a session."),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            string clientId = string.IsNullOrWhiteSpace(request.ClientId) ? "manual-client" : request.ClientId;
            string role = string.IsNullOrWhiteSpace(request.Role) ? "Reader" : request.Role;
            TimeSpan lifetime = TimeSpan.FromMinutes(Math.Clamp(request.LifetimeMinutes ?? 30, 1, 480));
            ClientSessionResult result = await _sessions.IssueAsync(
                    new ClientSessionRequest(clientId, role, lifetime),
                    ct)
                .ConfigureAwait(false);
            context.Items[IssuedSessionItemKey] = new ClientSessionSnapshot(
                TokenFingerprint: CreateTokenFingerprint(result.Token),
                ClientId: clientId.Trim(),
                Role: role.Trim(),
                ExpiresUtc: result.ExpiresUtc);
            return Results.Json(new LanSessionIssueResponse(result.Token, result.ExpiresUtc));
        });

        app.MapGet("/api/v1/catalogue", async (
            string? title,
            string? author,
            string? shelfId,
            int? status,
            int? page,
            int? pageSize,
            CancellationToken ct) =>
        {
            int pageNumber = Math.Clamp(page ?? 1, 1, 10_000);
            int size = Math.Clamp(pageSize ?? 50, 1, 100);
            int requested = checked((pageNumber * size) + 1);
            var fetched = new List<BookSummaryProjection>(requested);
            var filter = new CatalogueFilter(
                TitleContains: title,
                AuthorContains: author,
                ShelfId: shelfId,
                Status: status,
                MaxResults: requested);

            await foreach (BookSummaryProjection book in _catalogueReadModel.GetBookSummariesAsync(filter, ct)
                .ConfigureAwait(false))
            {
                fetched.Add(book);
            }

            List<BookSummaryProjection> items = fetched
                .Skip((pageNumber - 1) * size)
                .Take(size)
                .ToList();

            return Results.Json(new LanCataloguePage(
                Items: items.Select(MapSummary).ToList(),
                Page: pageNumber,
                PageSize: size,
                ReturnedCount: items.Count,
                HasMore: fetched.Count > pageNumber * size));
        });

        app.MapGet("/api/v1/catalogue/search", async (string? q, int? pageSize, CancellationToken ct) =>
        {
            int size = Math.Clamp(pageSize ?? 20, 1, 50);
            IReadOnlyList<MetadataSearchResult> results = await _metadataSearch.SearchAsync(q, ct)
                .ConfigureAwait(false);
            List<MetadataSearchResult> items = results.Take(size).ToList();
            return Results.Json(new LanCatalogueSearchPage(
                Query: q?.Trim() ?? string.Empty,
                Items: items,
                ReturnedCount: items.Count,
                HasMore: results.Count > size));
        });

        app.MapGet("/api/v1/catalogue/{bookId}", async (string bookId, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(bookId))
            {
                return Results.BadRequest(new LanHostError("invalid_book_id", "Book ID is required."));
            }

            BookDetailProjection? book = await _catalogueReadModel.GetBookDetailAsync(bookId, ct)
                .ConfigureAwait(false);
            return book is null
                ? Results.NotFound(new LanHostError("book_not_found", "The requested book was not found."))
                : Results.Json(MapDetail(book));
        });

        app.MapPut("/api/v1/profile/sync", async (HttpContext context, CancellationToken ct) =>
        {
            ClientSessionSnapshot session = GetAuthenticatedSession(context);
            if (context.Request.ContentLength is > MaxProfileSyncBlobBytes)
            {
                return Results.Json(
                    new LanHostError("sync_blob_too_large", "Profile sync blob exceeds the LAN Host size limit."),
                    statusCode: StatusCodes.Status413PayloadTooLarge);
            }

            using var output = new MemoryStream();
            await context.Request.Body.CopyToAsync(output, ct).ConfigureAwait(false);
            if (output.Length == 0)
            {
                return Results.BadRequest(new LanHostError("empty_sync_blob", "Profile sync blob content is required."));
            }

            if (output.Length > MaxProfileSyncBlobBytes)
            {
                return Results.Json(
                    new LanHostError("sync_blob_too_large", "Profile sync blob exceeds the LAN Host size limit."),
                    statusCode: StatusCodes.Status413PayloadTooLarge);
            }

            await _profileSyncBlobs
                .SaveAsync(
                    session.ClientId,
                    new HostProfileSyncBlob(
                        context.Request.ContentType ?? "application/octet-stream",
                        output.ToArray(),
                        DateTimeOffset.UtcNow),
                    ct)
                .ConfigureAwait(false);
            return Results.NoContent();
        });

        app.MapGet("/api/v1/profile/sync", async (HttpContext context, CancellationToken ct) =>
        {
            ClientSessionSnapshot session = GetAuthenticatedSession(context);
            HostProfileSyncBlob? blob = await _profileSyncBlobs
                .LoadAsync(session.ClientId, ct)
                .ConfigureAwait(false);
            return blob is null
                ? Results.NotFound(new LanHostError("sync_blob_not_found", "No profile sync blob has been stored for this client."))
                : Results.Bytes(blob.Content, blob.ContentType);
        });

        app.MapGet("/api/v1/assets/{assetClass}/{contentHash}", (
            string assetClass,
            string contentHash,
            string? variant) =>
        {
            if (!TryMapAssetClass(assetClass, out SidecarClass sidecarClass))
            {
                return Results.BadRequest(new LanHostError("invalid_asset_class", "Unknown LAN asset class."));
            }

            if (!IsSha256Hex(contentHash))
            {
                return Results.BadRequest(new LanHostError("invalid_content_hash", "Asset content hash must be 64 lowercase hex characters."));
            }

            if (!IsSafeVariant(variant))
            {
                return Results.BadRequest(new LanHostError("invalid_variant", "Asset variant is not valid."));
            }

            string path = _sidecarService.Resolve(contentHash, sidecarClass, variant);
            if (!File.Exists(path))
            {
                return Results.NotFound(new LanHostError("asset_not_found", "The requested asset was not found."));
            }

            return Results.File(path, "image/jpeg", enableRangeProcessing: true);
        });

        app.MapGet("/api/v1/books/{bookId}/page/{pageNumber:int}", async (
            string bookId,
            int pageNumber,
            int? widthPx,
            CancellationToken ct) =>
        {
            if (settings.ContentMode != HostContentDeliveryMode.PageRender)
            {
                return Results.Json(
                    new LanHostError(
                        "page_render_disabled",
                        "Page-render streaming is disabled for this Host. File-stream mode uses the raw PDF endpoint."),
                    statusCode: StatusCodes.Status403Forbidden);
            }

            if (pageNumber <= 0)
            {
                return Results.BadRequest(new LanHostError("invalid_page_number", "Page number must be 1 or greater."));
            }

            int width = Math.Clamp(widthPx ?? 1200, 320, 2400);
            if (!_pageRenderLimiter.TryAcquire(out IDisposable lease))
            {
                return Results.Json(
                    new LanHostError(
                        "page_render_busy",
                        "The Host is currently serving the maximum number of page-render requests. Try again shortly."),
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            RenderResult? result;
            using (lease)
            {
                result = await _pageRenderer
                    .RenderAsync(bookId, pageNumber, new RenderRequest(width), ct)
                    .ConfigureAwait(false);
            }

            if (result is null)
            {
                return Results.NotFound(new LanHostError("page_not_found", "The requested page was not found or cannot be rendered."));
            }

            return Results.Bytes(result.PngBytes, "image/png");
        });

        app.MapGet("/api/v1/books/{bookId}/file", async (string bookId, CancellationToken ct) =>
        {
            if (settings.ContentMode != HostContentDeliveryMode.FileStream)
            {
                return Results.Json(
                    new LanHostError(
                        "file_stream_disabled",
                        "Raw PDF file streaming is disabled for this Host. Page-render mode keeps PDF bytes on the Host."),
                    statusCode: StatusCodes.Status403Forbidden);
            }

            string? path = await _fileResolver.ResolveAsync(bookId, ct).ConfigureAwait(false);
            if (path is null)
            {
                return Results.NotFound(new LanHostError("book_file_not_found", "No streamable PDF file was found for this book."));
            }

            return Results.File(path, "application/pdf", enableRangeProcessing: true);
        });
    }

    private static bool IsPublicEndpoint(PathString path) =>
        path.StartsWithSegments("/api/v1/health", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/api/v1/auth/session", StringComparison.OrdinalIgnoreCase);

    private static ClientSessionSnapshot? GetIssuedSession(HttpContext context) =>
        context.Items.TryGetValue(IssuedSessionItemKey, out object? value)
            ? value as ClientSessionSnapshot
            : null;

    private static ClientSessionSnapshot GetAuthenticatedSession(HttpContext context) =>
        context.Items.TryGetValue(AuthenticatedSessionItemKey, out object? value) &&
        value is ClientSessionSnapshot session
            ? session
            : throw new InvalidOperationException("Authenticated LAN Host session was not available.");

    private static bool IsEnrollmentCodeValid(string? suppliedCode, string expectedCode)
    {
        if (string.IsNullOrWhiteSpace(suppliedCode))
        {
            return false;
        }

        byte[] suppliedBytes = Encoding.UTF8.GetBytes(suppliedCode.Trim());
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedCode);
        try
        {
            return suppliedBytes.Length == expectedBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
        }
        finally
        {
            Array.Clear(suppliedBytes);
            Array.Clear(expectedBytes);
        }
    }

    private static string? ReadBearerToken(HttpRequest request)
    {
        const string prefix = "Bearer ";
        string value = request.Headers.Authorization.ToString();
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..].Trim()
            : null;
    }

    private static string CreateTokenFingerprint(string token) =>
        ClientSessionService.HashToken(token)[..16];

    private static bool TryMapAssetClass(string value, out SidecarClass sidecarClass)
    {
        sidecarClass = value.ToLowerInvariant() switch
        {
            "cover" or "covers" => SidecarClass.Covers,
            "spine" or "spines" => SidecarClass.Spines,
            "thumb" or "thumbnail" or "thumbnails" => SidecarClass.Thumbnails,
            _ => default,
        };
        return sidecarClass is SidecarClass.Covers or SidecarClass.Spines or SidecarClass.Thumbnails;
    }

    private static bool IsSha256Hex(string value) =>
        value.Length == 64 && value.All(static ch => ch is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsSafeVariant(string? variant) =>
        variant is null ||
        (variant.Length <= 32 && variant.StartsWith('_') &&
         variant.All(static ch => ch is '_' or '-' or >= '0' and <= '9' or >= 'a' and <= 'z' or >= 'A' and <= 'Z'));

    private static LanCatalogueBook MapSummary(BookSummaryProjection book) =>
        new(
            BookId: book.BookId,
            Title: book.Title,
            Authors: book.Authors,
            Status: book.Status,
            Rating: book.Rating,
            ShelfIds: book.ShelfIds,
            ReadingProgressPct: book.ReadingProgressPct,
            IsAvailable: book.IsAvailable,
            Year: book.Year,
            ContentHash: book.Sha256Hash,
            Assets: BuildAssetLinks(book.Sha256Hash));

    private static LanBookDetail MapDetail(BookDetailProjection book) =>
        new(
            BookId: book.BookId,
            Title: book.Title,
            Authors: book.Authors,
            Year: book.Year,
            Isbn: book.Isbn,
            Doi: book.Doi,
            Rating: book.Rating,
            Status: book.Status,
            ContentHash: book.Sha256Hash,
            SizeBytes: book.SizeBytes,
            ReadingProgress: book.ReadingProgress,
            Annotations: book.Annotations,
            MetadataFields: book.MetadataFields
                .Where(static field => !IsLocalPathMetadataField(field.FieldName))
                .ToList(),
            ReadingMemory: book.ReadingMemory,
            IsOcrDerived: book.IsOcrDerived,
            IsPasswordProtected: book.IsPasswordProtected,
            Assets: BuildAssetLinks(book.Sha256Hash));

    private static bool IsLocalPathMetadataField(string fieldName) =>
        string.Equals(fieldName, "RelativePath", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(fieldName, "FileName", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(fieldName, "Path", StringComparison.OrdinalIgnoreCase);

    private static LanAssetLinks BuildAssetLinks(string? contentHash)
    {
        if (string.IsNullOrWhiteSpace(contentHash) || !IsSha256Hex(contentHash.ToLowerInvariant()))
        {
            return new LanAssetLinks(CoverUrl: null, SpineUrl: null, ThumbnailUrl: null);
        }

        string escapedHash = Uri.EscapeDataString(contentHash.ToLowerInvariant());
        return new LanAssetLinks(
            CoverUrl: $"/api/v1/assets/cover/{escapedHash}",
            SpineUrl: $"/api/v1/assets/spine/{escapedHash}",
            ThumbnailUrl: $"/api/v1/assets/thumb/{escapedHash}");
    }

    private static LanAuditRouteInfo ClassifyAuditRoute(HttpContext context, bool authenticated)
    {
        string path = context.Request.Path.Value ?? "/";
        string[] segments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        string actionPrefix = !authenticated &&
                              context.Response.StatusCode == StatusCodes.Status401Unauthorized &&
                              !IsPublicEndpoint(context.Request.Path)
            ? "RejectUnauthorized"
            : string.Empty;

        if (segments.Length < 3 ||
            !string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(segments[1], "v1", StringComparison.OrdinalIgnoreCase))
        {
            return new LanAuditRouteInfo(
                Action: string.IsNullOrEmpty(actionPrefix) ? "ServeRequest" : actionPrefix,
                ResourceType: "Unknown",
                ResourceId: null);
        }

        LanAuditRouteInfo route = segments[2].ToLowerInvariant() switch
        {
            "health" => new LanAuditRouteInfo("CheckHealth", "Health", null),
            "auth" when segments.Length >= 4 &&
                        string.Equals(segments[3], "session", StringComparison.OrdinalIgnoreCase) =>
                new LanAuditRouteInfo("IssueSession", "Session", null),
            "catalogue" when segments.Length == 3 =>
                new LanAuditRouteInfo("ListCatalogue", "Catalogue", null),
            "catalogue" when segments.Length >= 4 &&
                             string.Equals(segments[3], "search", StringComparison.OrdinalIgnoreCase) =>
                new LanAuditRouteInfo("SearchCatalogue", "CatalogueSearch", null),
            "catalogue" when segments.Length >= 4 =>
                new LanAuditRouteInfo("GetBookDetail", "Book", segments[3]),
            "profile" when segments.Length >= 4 &&
                           string.Equals(segments[3], "sync", StringComparison.OrdinalIgnoreCase) =>
                new LanAuditRouteInfo(
                    context.Request.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase)
                        ? "UploadProfileSync"
                        : "DownloadProfileSync",
                    "ProfileSync",
                    GetAuditClientId(context)),
            "assets" when segments.Length >= 5 =>
                new LanAuditRouteInfo("ServeAsset", "Asset", $"{segments[3]}:{segments[4]}"),
            "books" when segments.Length >= 6 &&
                         string.Equals(segments[4], "page", StringComparison.OrdinalIgnoreCase) =>
                new LanAuditRouteInfo("RenderPage", "BookPage", $"{segments[3]}:page:{segments[5]}"),
            "books" when segments.Length >= 5 &&
                         string.Equals(segments[4], "file", StringComparison.OrdinalIgnoreCase) =>
                new LanAuditRouteInfo("StreamFile", "BookFile", segments[3]),
            _ => new LanAuditRouteInfo("ServeRequest", "Unknown", null),
        };

        return string.IsNullOrEmpty(actionPrefix)
            ? route
            : route with { Action = actionPrefix };
    }

    private static string? GetAuditClientId(HttpContext context) =>
        context.Items.TryGetValue(AuthenticatedSessionItemKey, out object? value) &&
        value is ClientSessionSnapshot session
            ? session.ClientId
            : null;

    private async Task AppendAuditAsync(
        HttpContext context,
        string? token,
        ClientSessionSnapshot? session,
        bool authenticated,
        HostContentDeliveryMode contentMode,
        long started)
    {
        string path = context.Request.Path.Value ?? "/";
        LanAuditRouteInfo route = ClassifyAuditRoute(context, authenticated);
        string? tokenFingerprint = session?.TokenFingerprint ??
                                   (string.IsNullOrWhiteSpace(token) ? null : CreateTokenFingerprint(token));
        string? actorId = session is not null
            ? $"client:{session.ClientId}"
            : tokenFingerprint is null ? null : $"session:{tokenFingerprint}";
        var payload = new
        {
            action = route.Action,
            resourceType = route.ResourceType,
            resourceId = route.ResourceId,
            method = context.Request.Method,
            path,
            statusCode = context.Response.StatusCode,
            remoteIpAddress = context.Connection.RemoteIpAddress?.ToString(),
            elapsedMs = Math.Max(0, Environment.TickCount64 - started),
            authenticated,
            contentMode = contentMode.ToString(),
            clientId = session?.ClientId,
            role = session?.Role,
            sessionFingerprint = tokenFingerprint,
        };

        await _audit.AppendAsync(
                new AuditEvent
                {
                    Id = Guid.NewGuid().ToString("N"),
                    EventType = "LanHostRequestServed",
                    EntityId = path,
                    ActorId = actorId,
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Payload = JsonSerializer.Serialize(payload),
                },
                context.RequestAborted)
            .ConfigureAwait(false);
    }

    private sealed record LanAuditRouteInfo(
        string Action,
        string ResourceType,
        string? ResourceId);

    private sealed record LanHostHealthResponse(
        string State,
        int Port,
        string BindAddress,
        string CertificateFingerprint,
        bool RequiresAuth);

    private sealed record LanSessionIssueRequest(
        string? ClientId,
        string? Role,
        int? LifetimeMinutes,
        string? EnrollmentCode);

    private sealed record LanSessionIssueResponse(
        string Token,
        DateTimeOffset ExpiresUtc);

    private sealed record LanCataloguePage(
        IReadOnlyList<LanCatalogueBook> Items,
        int Page,
        int PageSize,
        int ReturnedCount,
        bool HasMore);

    private sealed record LanCatalogueSearchPage(
        string Query,
        IReadOnlyList<MetadataSearchResult> Items,
        int ReturnedCount,
        bool HasMore);

    private sealed record LanCatalogueBook(
        string BookId,
        string? Title,
        IReadOnlyList<string> Authors,
        int Status,
        int? Rating,
        IReadOnlyList<string> ShelfIds,
        double? ReadingProgressPct,
        bool IsAvailable,
        int? Year,
        string? ContentHash,
        LanAssetLinks Assets);

    private sealed record LanBookDetail(
        string BookId,
        string? Title,
        IReadOnlyList<string> Authors,
        int? Year,
        string? Isbn,
        string? Doi,
        int? Rating,
        int Status,
        string? ContentHash,
        long? SizeBytes,
        ReadingProgressProjection? ReadingProgress,
        int Annotations,
        IReadOnlyList<MetadataFieldProjection> MetadataFields,
        ReadingMemorySummaryProjection? ReadingMemory,
        bool IsOcrDerived,
        bool IsPasswordProtected,
        LanAssetLinks Assets);

    private sealed record LanAssetLinks(
        string? CoverUrl,
        string? SpineUrl,
        string? ThumbnailUrl);

    private sealed record LanHostError(
        string Code,
        string Message);
}
