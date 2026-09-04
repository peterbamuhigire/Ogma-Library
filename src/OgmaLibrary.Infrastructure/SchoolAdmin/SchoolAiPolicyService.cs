using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.SchoolAdmin;

/// <summary>SQLite-backed classroom AI policy and quota service.</summary>
internal sealed class SchoolAiPolicyService : ISchoolAiPolicyService, IDisposable
{
    internal const int DefaultPerStudentDailyTokenBudget = 10_000;
    internal const int DefaultClassDailyTokenBudget = 500_000;
    internal const int DefaultPerStudentQueriesPerMinute = 5;

    private const int MaxReservationAttempts = 8;
    private readonly IDbContextFactory<CatalogueDbContext> _contextFactory;
    private readonly SemaphoreSlim _quotaGate = new(1, 1);

    public SchoolAiPolicyService(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<SchoolAiPolicy> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            try
            {
                SchoolAiEntitlementRow? row = await context.SchoolAiEntitlements
                    .AsNoTracking()
                    .OrderBy(entitlement => entitlement.ProfileId)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
                return CreatePolicy(
                    row?.DailyTokenBudget ?? DefaultPerStudentDailyTokenBudget,
                    row?.ClassDailyTokenBudget ?? DefaultClassDailyTokenBudget,
                    row?.RateLimitQueriesPerMin ?? DefaultPerStudentQueriesPerMinute);
            }
            catch (SqliteException error) when (IsMissingTable(error))
            {
                return CreatePolicy(
                    DefaultPerStudentDailyTokenBudget,
                    DefaultClassDailyTokenBudget,
                    DefaultPerStudentQueriesPerMinute);
            }
        }
    }

    public async Task SavePolicyAsync(SchoolAiPolicy policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ValidatePolicy(policy);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            List<SchoolAiEntitlementRow> rows = await context.SchoolAiEntitlements
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (SchoolAiEntitlementRow row in rows)
            {
                row.DailyTokenBudget = policy.PerStudentDailyTokenBudget;
                row.ClassDailyTokenBudget = policy.ClassDailyTokenBudget;
                row.RateLimitQueriesPerMin = policy.PerStudentQueriesPerMinute;
                row.UpdatedUtc = now;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<SchoolAiQuotaDecision> CheckAndReserveQuotaAsync(
        Guid profileId,
        int estimatedTokens,
        CancellationToken cancellationToken = default)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        if (estimatedTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedTokens), estimatedTokens, "Estimated tokens cannot be negative.");
        }

        for (int attempt = 0; attempt < MaxReservationAttempts; attempt++)
        {
            try
            {
                await _quotaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    return await ReserveQuotaOnceAsync(profileId, estimatedTokens, cancellationToken)
                        .ConfigureAwait(false);
                }
                finally
                {
                    _quotaGate.Release();
                }
            }
            catch (DbUpdateException) when (attempt + 1 < MaxReservationAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10 * (attempt + 1)), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (SqliteException error) when (error.SqliteErrorCode == 5 && attempt + 1 < MaxReservationAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10 * (attempt + 1)), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("Could not reserve school AI quota after repeated attempts.");
    }

    private async Task<SchoolAiQuotaDecision> ReserveQuotaOnceAsync(
        Guid profileId,
        int estimatedTokens,
        CancellationToken cancellationToken)
    {
        string profileKey = profileId.ToString("D");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string date = now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        DateTimeOffset resetUtc = new(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);

        CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            IDbContextTransaction transaction = await context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                .ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                SchoolAiEntitlementRow? entitlement = await context.SchoolAiEntitlements
                    .FirstOrDefaultAsync(row => row.ProfileId == profileKey, cancellationToken)
                    .ConfigureAwait(false);
                if (entitlement is null)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return Denied(0, 0, resetUtc, "Profile is not enrolled for school AI.");
                }

                AiUsageLedgerRow? profileLedger = await context.AiUsageLedger
                    .FirstOrDefaultAsync(row => row.ProfileId == profileKey && row.Date == date, cancellationToken)
                    .ConfigureAwait(false);
                int classTokensUsed = await context.AiUsageLedger
                    .Where(row => row.Date == date)
                    .SumAsync(row => row.TokensUsed, cancellationToken)
                    .ConfigureAwait(false);
                int profileTokensUsed = profileLedger?.TokensUsed ?? 0;
                int remainingStudentTokens = Math.Max(0, entitlement.DailyTokenBudget - profileTokensUsed);
                int remainingClassTokens = Math.Max(0, entitlement.ClassDailyTokenBudget - classTokensUsed);

                if (estimatedTokens > remainingStudentTokens)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return Denied(
                        remainingStudentTokens,
                        remainingClassTokens,
                        resetUtc,
                        "Student AI token budget is exhausted.");
                }

                if (estimatedTokens > remainingClassTokens)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return Denied(
                        remainingStudentTokens,
                        remainingClassTokens,
                        resetUtc,
                        "Class AI token budget is exhausted.");
                }

                if (profileLedger is null)
                {
                    profileLedger = new AiUsageLedgerRow
                    {
                        Id = $"{profileKey}:{date}",
                        ProfileId = profileKey,
                        Date = date,
                    };
                    context.AiUsageLedger.Add(profileLedger);
                }

                profileLedger.TokensUsed += estimatedTokens;
                profileLedger.QueryCount++;
                profileLedger.UpdatedUtc = now;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                return new SchoolAiQuotaDecision(
                    IsAllowed: true,
                    Math.Max(0, entitlement.DailyTokenBudget - profileLedger.TokensUsed),
                    Math.Max(0, entitlement.ClassDailyTokenBudget - classTokensUsed - estimatedTokens),
                    resetUtc,
                    Reason: null);
            }
        }
    }

    private static SchoolAiQuotaDecision Denied(
        int remainingStudentTokens,
        int remainingClassTokens,
        DateTimeOffset resetUtc,
        string reason) =>
        new(
            IsAllowed: false,
            remainingStudentTokens,
            remainingClassTokens,
            resetUtc,
            reason);

    private static SchoolAiPolicy CreatePolicy(
        int perStudentDailyTokenBudget,
        int classDailyTokenBudget,
        int perStudentQueriesPerMinute) =>
        new(
            AiPrivacyTier.MetadataOnly,
            ContentAwareEnabled: false,
            perStudentDailyTokenBudget,
            classDailyTokenBudget,
            perStudentQueriesPerMinute,
            AnswerModeEnabled: false);

    private static bool IsMissingTable(SqliteException error) =>
        error.SqliteErrorCode == 1 &&
        error.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase);

    private static void ValidatePolicy(SchoolAiPolicy policy)
    {
        if (policy.DefaultTier != AiPrivacyTier.MetadataOnly ||
            policy.ContentAwareEnabled ||
            policy.AnswerModeEnabled)
        {
            throw new InvalidOperationException(
                "Classroom AI currently supports metadata-only requests without answer mode.");
        }

        if (policy.PerStudentDailyTokenBudget < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(policy), policy.PerStudentDailyTokenBudget, "Student token budget cannot be negative.");
        }

        if (policy.ClassDailyTokenBudget < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(policy), policy.ClassDailyTokenBudget, "Class token budget cannot be negative.");
        }

        if (policy.PerStudentQueriesPerMinute < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(policy), policy.PerStudentQueriesPerMinute, "Rate limit cannot be negative.");
        }
    }

    public void Dispose() => _quotaGate.Dispose();
}
