using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.SchoolAdmin;

/// <summary>SQLite-backed school AI usage dashboard aggregation.</summary>
internal sealed class SchoolUsageDashboardService : IUsageDashboardService
{
    private readonly IDbContextFactory<CatalogueDbContext> _contextFactory;

    public SchoolUsageDashboardService(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<IReadOnlyList<UsageDashboardEntry>> GetSummaryAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        if (toUtc < fromUtc)
        {
            throw new ArgumentException("End date must be after start date.", nameof(toUtc));
        }

        string startDate = FormatDate(fromUtc);
        string endDate = FormatDate(toUtc);
        CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            List<EnrolledProfileRow> profiles = await context.EnrolledProfiles
                .AsNoTracking()
                .OrderBy(profile => profile.DisplayName)
                .ThenBy(profile => profile.ProfileId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            List<SchoolAiEntitlementRow> entitlements = await context.SchoolAiEntitlements
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            List<AiUsageLedgerRow> ledgers = await context.AiUsageLedger
                .AsNoTracking()
                .Where(row => row.Date.CompareTo(startDate) >= 0 &&
                    row.Date.CompareTo(endDate) <= 0)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            Dictionary<string, SchoolAiEntitlementRow> entitlementsByProfile = entitlements
                .ToDictionary(row => row.ProfileId, StringComparer.Ordinal);
            Dictionary<string, List<AiUsageLedgerRow>> ledgersByProfile = ledgers
                .GroupBy(row => row.ProfileId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
            HashSet<string> profileIds = profiles.Select(profile => profile.ProfileId)
                .Concat(ledgers.Select(ledger => ledger.ProfileId))
                .ToHashSet(StringComparer.Ordinal);
            Dictionary<string, EnrolledProfileRow> profilesById = profiles
                .ToDictionary(profile => profile.ProfileId, StringComparer.Ordinal);

            return profileIds
                .Select(profileId => CreateEntry(
                    profileId,
                    profilesById.GetValueOrDefault(profileId),
                    entitlementsByProfile.GetValueOrDefault(profileId),
                    ledgersByProfile.GetValueOrDefault(profileId) ?? []))
                .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.ProfileId)
                .ToList();
        }
    }

    private static UsageDashboardEntry CreateEntry(
        string profileId,
        EnrolledProfileRow? profile,
        SchoolAiEntitlementRow? entitlement,
        List<AiUsageLedgerRow> ledgers)
    {
        int tokensUsed = ledgers.Sum(ledger => ledger.TokensUsed);
        int queryCount = ledgers.Sum(ledger => ledger.QueryCount);
        decimal estimatedCostUsd = ledgers.Sum(ledger => ledger.EstimatedCostUsd);
        int dailyBudget = entitlement?.DailyTokenBudget ?? SchoolAiPolicyService.DefaultPerStudentDailyTokenBudget;
        double quotaPercent = dailyBudget <= 0
            ? tokensUsed > 0 ? 100d : 0d
            : Math.Min(100d, tokensUsed * 100d / dailyBudget);
        DateTimeOffset? lastQueryUtc = ledgers.Count == 0
            ? null
            : ledgers.Max(ledger => ledger.UpdatedUtc);

        return new UsageDashboardEntry(
            Guid.TryParse(profileId, out Guid parsedProfileId) ? parsedProfileId : Guid.Empty,
            profile?.DisplayName ?? "Unknown profile",
            queryCount,
            tokensUsed,
            estimatedCostUsd,
            quotaPercent,
            lastQueryUtc);
    }

    private static string FormatDate(DateTimeOffset value) =>
        value.UtcDateTime.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
