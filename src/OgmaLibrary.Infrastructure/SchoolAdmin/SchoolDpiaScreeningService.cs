using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.SchoolAdmin;

/// <summary>Conservative classroom DPIA screening for school-managed AI requests.</summary>
internal sealed class SchoolDpiaScreeningService : IDpiaScreeningService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<CatalogueDbContext> _contextFactory;

    public SchoolDpiaScreeningService(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<DpiaScreeningResult> CheckAsync(
        DpiaScreeningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProfileId == Guid.Empty)
        {
            throw new ArgumentException("Profile id is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.PayloadScope))
        {
            throw new ArgumentException("Payload scope is required.", nameof(request));
        }

        DateTimeOffset checkedUtc = DateTimeOffset.UtcNow;
        DpiaScreeningResult result = Screen(request, checkedUtc);
        await TryWriteAuditAsync(request, result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static DpiaScreeningResult Screen(DpiaScreeningRequest request, DateTimeOffset checkedUtc)
    {
        bool isMinorOrUnknown = request.BirthYear is null ||
            checkedUtc.Year - request.BirthYear.Value < 18;

        return request.Tier switch
        {
            AiPrivacyTier.Offline => Approved("Offline tier has no off-device AI egress.", checkedUtc),
            AiPrivacyTier.LocalOllama => Approved("Local Ollama tier has no off-device AI egress.", checkedUtc),
            AiPrivacyTier.MetadataOnly => Approved("Metadata-only classroom AI is permitted by the default school policy.", checkedUtc),
            AiPrivacyTier.ContentAware when isMinorOrUnknown => Disqualified(
                "Content-aware classroom AI for minors or unknown-age profiles requires explicit school DPIA approval.",
                checkedUtc),
            AiPrivacyTier.ContentAware => Disqualified(
                "Content-aware classroom AI requires explicit school DPIA approval before provider egress.",
                checkedUtc),
            _ => Disqualified("Unknown AI privacy tier.", checkedUtc),
        };
    }

    private async Task TryWriteAuditAsync(
        DpiaScreeningRequest request,
        DpiaScreeningResult result,
        CancellationToken cancellationToken)
    {
        CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            context.AuditEvents.Add(new AuditEventRow
            {
                EventType = "SchoolDpiaScreened",
                EntityId = request.ProfileId.ToString("D"),
                EntityType = "SchoolAiDpia",
                ActorId = $"profile:{request.ProfileId:D}",
                AfterJson = JsonSerializer.Serialize(new
                {
                    tier = request.Tier.ToString(),
                    payloadScope = request.PayloadScope.Trim(),
                    birthYearProvided = request.BirthYear is not null,
                    decision = result.Decision.ToString(),
                    reason = result.Reason,
                }, JsonOptions),
                Timestamp = result.CheckedUtc,
                IsLocalOnly = true,
            });

            try
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqliteException error) when (IsMissingTable(error))
            {
            }
            catch (DbUpdateException error) when (error.InnerException is SqliteException sqlite && IsMissingTable(sqlite))
            {
            }
        }
    }

    private static DpiaScreeningResult Approved(string reason, DateTimeOffset checkedUtc) =>
        new(DpiaScreeningDecision.Approved, reason, checkedUtc);

    private static DpiaScreeningResult Disqualified(string reason, DateTimeOffset checkedUtc) =>
        new(DpiaScreeningDecision.Disqualified, reason, checkedUtc);

    private static bool IsMissingTable(SqliteException error) =>
        error.SqliteErrorCode == 1 &&
        error.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase);
}
