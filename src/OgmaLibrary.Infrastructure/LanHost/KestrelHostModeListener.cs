using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Domain;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Loopback HTTPS listener for the first Phase 16 Host-mode endpoints.</summary>
internal sealed class KestrelHostModeListener : IHostModeListener
{
    private readonly ICatalogueReadModel _catalogueReadModel;
    private readonly IMetadataSearchService _metadataSearch;
    private readonly ISidecarService _sidecarService;
    private readonly ILanBookFileResolver _fileResolver;
    private readonly IClientSessionService _sessions;
    private readonly IHostServerCertificateProvider _certificates;
    private readonly IAuditRepository _audit;
    private WebApplication? _app;

    public KestrelHostModeListener(
        ICatalogueReadModel catalogueReadModel,
        IMetadataSearchService metadataSearch,
        ISidecarService sidecarService,
        ILanBookFileResolver fileResolver,
        IClientSessionService sessions,
        IHostServerCertificateProvider certificates,
        IAuditRepository audit)
    {
        _catalogueReadModel = catalogueReadModel ?? throw new ArgumentNullException(nameof(catalogueReadModel));
        _metadataSearch = metadataSearch ?? throw new ArgumentNullException(nameof(metadataSearch));
        _sidecarService = sidecarService ?? throw new ArgumentNullException(nameof(sidecarService));
        _fileResolver = fileResolver ?? throw new ArgumentNullException(nameof(fileResolver));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _certificates = certificates ?? throw new ArgumentNullException(nameof(certificates));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    /// <inheritdoc />
    public async Task StartAsync(
        HostModeSettings settings,
        string certificateFingerprint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateFingerprint);
        await StopAsync(cancellationToken).ConfigureAwait(false);

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(
                IPAddress.Loopback,
                settings.Port,
                listen => listen.UseHttps(_certificates.LoadOrCreateCertificateAsync(cancellationToken)
                    .GetAwaiter()
                    .GetResult()));
        });
        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.AddServerHeader = false;
        });

        WebApplication app = builder.Build();
        ConfigurePipeline(app, settings, certificateFingerprint);

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
        string certificateFingerprint)
    {
        app.Use(async (context, next) =>
        {
            long started = Environment.TickCount64;
            string? token = ReadBearerToken(context.Request);
            bool authenticated = false;

            if (IsPublicEndpoint(context.Request.Path))
            {
                await next(context).ConfigureAwait(false);
                await AppendAuditAsync(context, token, authenticated, settings.ContentMode, started).ConfigureAwait(false);
                return;
            }

            if (token is null || !await _sessions.IsValidAsync(token, context.RequestAborted).ConfigureAwait(false))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new LanHostError("unauthorized", "A valid LAN Host session token is required."),
                    context.RequestAborted).ConfigureAwait(false);
                await AppendAuditAsync(context, token, authenticated, settings.ContentMode, started).ConfigureAwait(false);
                return;
            }

            authenticated = true;
            await next(context).ConfigureAwait(false);
            await AppendAuditAsync(context, token, authenticated, settings.ContentMode, started).ConfigureAwait(false);
        });

        app.MapGet("/api/v1/health", () => Results.Json(new LanHostHealthResponse(
            State: "running",
            Port: settings.Port,
            CertificateFingerprint: certificateFingerprint,
            RequiresAuth: true)));

        app.MapPost("/api/v1/auth/session", async (LanSessionIssueRequest request, CancellationToken ct) =>
        {
            string clientId = string.IsNullOrWhiteSpace(request.ClientId) ? "manual-client" : request.ClientId;
            string role = string.IsNullOrWhiteSpace(request.Role) ? "Reader" : request.Role;
            TimeSpan lifetime = TimeSpan.FromMinutes(Math.Clamp(request.LifetimeMinutes ?? 30, 1, 480));
            ClientSessionResult result = await _sessions.IssueAsync(
                    new ClientSessionRequest(clientId, role, lifetime),
                    ct)
                .ConfigureAwait(false);
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
                Items: items,
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
                : Results.Json(book);
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

    private static string? ReadBearerToken(HttpRequest request)
    {
        const string prefix = "Bearer ";
        string value = request.Headers.Authorization.ToString();
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..].Trim()
            : null;
    }

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

    private async Task AppendAuditAsync(
        HttpContext context,
        string? token,
        bool authenticated,
        HostContentDeliveryMode contentMode,
        long started)
    {
        string path = context.Request.Path.Value ?? "/";
        string? actorId = string.IsNullOrWhiteSpace(token)
            ? null
            : $"session:{ClientSessionService.HashToken(token)[..16]}";
        var payload = new
        {
            method = context.Request.Method,
            path,
            statusCode = context.Response.StatusCode,
            remoteIpAddress = context.Connection.RemoteIpAddress?.ToString(),
            elapsedMs = Math.Max(0, Environment.TickCount64 - started),
            authenticated,
            contentMode = contentMode.ToString(),
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

    private sealed record LanHostHealthResponse(
        string State,
        int Port,
        string CertificateFingerprint,
        bool RequiresAuth);

    private sealed record LanSessionIssueRequest(
        string? ClientId,
        string? Role,
        int? LifetimeMinutes);

    private sealed record LanSessionIssueResponse(
        string Token,
        DateTimeOffset ExpiresUtc);

    private sealed record LanCataloguePage(
        IReadOnlyList<BookSummaryProjection> Items,
        int Page,
        int PageSize,
        int ReturnedCount,
        bool HasMore);

    private sealed record LanCatalogueSearchPage(
        string Query,
        IReadOnlyList<MetadataSearchResult> Items,
        int ReturnedCount,
        bool HasMore);

    private sealed record LanHostError(
        string Code,
        string Message);
}
