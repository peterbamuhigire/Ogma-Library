using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>
/// Persists the active semantic-index pointer and performs atomic staging
/// transitions. Vector rows are retained across promotion until an explicit
/// cleanup/erasure operation removes them.
/// </summary>
public sealed class EmbeddingIndexLifecycleService : IEmbeddingIndexLifecycleService, IDisposable
{
    private const string StateKey = "semantic";
    private const string DefaultActiveIndexVersion = "fts5-v1";

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly SemaphoreSlim _transitionGate = new(1, 1);

    /// <summary>Initializes the service for application use.</summary>
    [ActivatorUtilitiesConstructor]
    public EmbeddingIndexLifecycleService(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <summary>Initializes the service for tests sharing one context.</summary>
    internal EmbeddingIndexLifecycleService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<EmbeddingIndexState> GetStateAsync(CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        EmbeddingIndexStateRow row = await GetOrCreateAsync(lease.Context, cancellationToken)
            .ConfigureAwait(false);
        return Map(row);
    }

    /// <inheritdoc />
    public async Task<EmbeddingIndexState> BeginRebuildAsync(
        string stagingIndexVersion,
        CancellationToken cancellationToken)
    {
        ValidateIndexVersion(stagingIndexVersion);
        await _transitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
            EmbeddingIndexStateRow row = await GetOrCreateAsync(lease.Context, cancellationToken)
                .ConfigureAwait(false);
            if (string.Equals(row.ActiveIndexVersion, stagingIndexVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The staging semantic index must differ from the active index.");
            }

            if (row.StagingIndexVersion is not null &&
                !string.Equals(row.StagingIndexVersion, stagingIndexVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Semantic index rebuild '{row.StagingIndexVersion}' is already in progress.");
            }

            row.StagingIndexVersion = stagingIndexVersion;
            row.UpdatedUtc = DateTimeOffset.UtcNow;
            await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Map(row);
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<EmbeddingIndexState> PromoteAsync(
        string stagingIndexVersion,
        CancellationToken cancellationToken)
    {
        ValidateIndexVersion(stagingIndexVersion);
        await _transitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
            EmbeddingIndexStateRow row = await GetOrCreateAsync(lease.Context, cancellationToken)
                .ConfigureAwait(false);
            if (!string.Equals(row.StagingIndexVersion, stagingIndexVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The requested semantic index is not the active staging generation.");
            }

            row.ActiveIndexVersion = stagingIndexVersion;
            row.StagingIndexVersion = null;
            row.UpdatedUtc = DateTimeOffset.UtcNow;
            await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Map(row);
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<EmbeddingIndexState> AbandonAsync(
        string stagingIndexVersion,
        CancellationToken cancellationToken)
    {
        ValidateIndexVersion(stagingIndexVersion);
        await _transitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
            EmbeddingIndexStateRow row = await GetOrCreateAsync(lease.Context, cancellationToken)
                .ConfigureAwait(false);
            if (row.StagingIndexVersion is not null &&
                !string.Equals(row.StagingIndexVersion, stagingIndexVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The requested semantic index is not the active staging generation.");
            }

            row.StagingIndexVersion = null;
            row.UpdatedUtc = DateTimeOffset.UtcNow;
            await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Map(row);
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _transitionGate.Dispose();

    private static async Task<EmbeddingIndexStateRow> GetOrCreateAsync(
        CatalogueDbContext context,
        CancellationToken cancellationToken)
    {
        EmbeddingIndexStateRow? row = await context.EmbeddingIndexState
            .SingleOrDefaultAsync(candidate => candidate.StateKey == StateKey, cancellationToken)
            .ConfigureAwait(false);
        if (row is not null)
        {
            return row;
        }

        row = new EmbeddingIndexStateRow
        {
            StateKey = StateKey,
            ActiveIndexVersion = DefaultActiveIndexVersion,
            UpdatedUtc = DateTimeOffset.UtcNow,
        };
        context.EmbeddingIndexState.Add(row);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return row;
    }

    private static EmbeddingIndexState Map(EmbeddingIndexStateRow row) =>
        new(row.ActiveIndexVersion, row.StagingIndexVersion, row.UpdatedUtc);

    private static void ValidateIndexVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 128 || value.Any(char.IsControl) || value.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Semantic index version must be a bounded token.", nameof(value));
        }
    }

    private async ValueTask<ContextLease> CreateLeaseAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is not null)
        {
            CatalogueDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);
            return new ContextLease(context, ownsContext: true);
        }

        return new ContextLease(_context!, ownsContext: false);
    }

    private readonly struct ContextLease : IDisposable
    {
        public ContextLease(CatalogueDbContext context, bool ownsContext)
        {
            Context = context;
            _ownsContext = ownsContext;
        }

        private readonly bool _ownsContext;

        public CatalogueDbContext Context { get; }

        public void Dispose()
        {
            if (_ownsContext)
            {
                Context.Dispose();
            }
        }
    }
}
