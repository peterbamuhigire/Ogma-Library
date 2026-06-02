using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.Infrastructure.SchoolAdmin;

/// <summary>SQLite-backed erasable classroom AI history management.</summary>
internal sealed class SchoolAiHistoryManagementService : ISchoolAiHistoryManagementService
{
    private readonly IDbContextFactory<CatalogueDbContext> _contextFactory;

    public SchoolAiHistoryManagementService(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<SchoolAiHistoryPurgeResult> PurgeInstitutionHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset purgedUtc = DateTimeOffset.UtcNow;
        CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            IDbContextTransaction transaction = await context.Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                int historyRows = await context.AiQueryHistory
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
                int ledgerRows = await context.AiUsageLedger
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new SchoolAiHistoryPurgeResult(historyRows, ledgerRows, purgedUtc);
            }
        }
    }
}
