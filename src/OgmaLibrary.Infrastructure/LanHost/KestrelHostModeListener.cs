using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.LanHost;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Loopback HTTPS listener for the first Phase 16 Host-mode endpoints.</summary>
internal sealed class KestrelHostModeListener : IHostModeListener
{
    private readonly ICatalogueReadModel _catalogueReadModel;
    private readonly IClientSessionService _sessions;
    private readonly IHostServerCertificateProvider _certificates;
    private WebApplication? _app;

    public KestrelHostModeListener(
        ICatalogueReadModel catalogueReadModel,
        IClientSessionService sessions,
        IHostServerCertificateProvider certificates)
    {
        _catalogueReadModel = catalogueReadModel ?? throw new ArgumentNullException(nameof(catalogueReadModel));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _certificates = certificates ?? throw new ArgumentNullException(nameof(certificates));
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
            if (IsPublicEndpoint(context.Request.Path))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            string? token = ReadBearerToken(context.Request);
            if (token is null || !await _sessions.IsValidAsync(token, context.RequestAborted).ConfigureAwait(false))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new LanHostError("unauthorized", "A valid LAN Host session token is required."),
                    context.RequestAborted).ConfigureAwait(false);
                return;
            }

            await next(context).ConfigureAwait(false);
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
            int? status,
            int? pageSize,
            CancellationToken ct) =>
        {
            int size = Math.Clamp(pageSize ?? 50, 1, 100);
            var items = new List<BookSummaryProjection>(size);
            var filter = new CatalogueFilter(
                TitleContains: title,
                AuthorContains: author,
                Status: status,
                MaxResults: size);

            await foreach (BookSummaryProjection book in _catalogueReadModel.GetBookSummariesAsync(filter, ct)
                .ConfigureAwait(false))
            {
                items.Add(book);
            }

            return Results.Json(new LanCataloguePage(items, items.Count));
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
        int Count);

    private sealed record LanHostError(
        string Code,
        string Message);
}
