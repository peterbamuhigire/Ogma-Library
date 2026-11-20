using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>
/// Decides where the background worker embeds next (Sept-23 Phase 14, slice 4). When the
/// active generation was built by the current provider and model (or is empty) new chunks are
/// embedded into it in place. When the provider or model changed, a staged regeneration runs
/// through <see cref="IEmbeddingIndexRebuildService"/> while searches keep the old generation,
/// and the active pointer swaps atomically once coverage is complete. A staging generation
/// built by another model is abandoned and its vectors deleted, so a generation never mixes
/// models or dimensions.
/// </summary>
public sealed class SemanticGenerationCoordinator : IEmbeddingGenerationService
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly IEmbeddingProvider _provider;
    private readonly IStagedEmbeddingGenerationService _generation;
    private readonly IEmbeddingIndexLifecycleService _lifecycle;
    private readonly IEmbeddingIndexRebuildService _rebuild;

    /// <summary>Initializes the coordinator for application use.</summary>
    [ActivatorUtilitiesConstructor]
    public SemanticGenerationCoordinator(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IEmbeddingProvider provider,
        IStagedEmbeddingGenerationService generation,
        IEmbeddingIndexLifecycleService lifecycle,
        IEmbeddingIndexRebuildService rebuild)
        : this(provider, generation, lifecycle, rebuild)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <summary>Initializes the coordinator for tests sharing one context.</summary>
    internal SemanticGenerationCoordinator(
        CatalogueDbContext context,
        IEmbeddingProvider provider,
        IStagedEmbeddingGenerationService generation,
        IEmbeddingIndexLifecycleService lifecycle,
        IEmbeddingIndexRebuildService rebuild)
        : this(provider, generation, lifecycle, rebuild)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    private SemanticGenerationCoordinator(
        IEmbeddingProvider provider,
        IStagedEmbeddingGenerationService generation,
        IEmbeddingIndexLifecycleService lifecycle,
        IEmbeddingIndexRebuildService rebuild)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(rebuild);
        _provider = provider;
        _generation = generation;
        _lifecycle = lifecycle;
        _rebuild = rebuild;
    }

    /// <inheritdoc />
    public async Task<EmbeddingGenerationBatchResult> GenerateNextBatchAsync(
        int maxChunks,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxChunks);
        if (string.IsNullOrEmpty(_provider.ModelId) || _provider.Dimensions <= 0)
        {
            // Off, or no model known: leave every generation exactly as it is.
            return new EmbeddingGenerationBatchResult(0, 0, 0, 0, ProviderUnavailable: true);
        }

        EmbeddingGenerationIdentity current = EmbeddingGenerationIdentity.Of(_provider);
        EmbeddingIndexState state = await _lifecycle.GetStateAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EmbeddingGenerationIdentity> active;
        IReadOnlyList<EmbeddingGenerationIdentity> staging = [];
        using (ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false))
        {
            active = await EmbeddingGenerationIdentity.ReadAsync(lease.Context, state.ActiveIndexVersion, cancellationToken)
                .ConfigureAwait(false);
            if (state.StagingIndexVersion is not null)
            {
                staging = await EmbeddingGenerationIdentity.ReadAsync(lease.Context, state.StagingIndexVersion, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        bool activeIsCurrent = active.Count == 0 || active.Contains(current);
        if (state.StagingIndexVersion is not null)
        {
            bool stagingIsStale = staging.Any(identity => identity != current) ||
                (activeIsCurrent && active.Count > 0 && staging.Count == 0);
            if (!stagingIsStale)
            {
                return Map(await _rebuild.RunNextBatchAsync(maxChunks, cancellationToken).ConfigureAwait(false));
            }

            await DiscardStagingAsync(state.StagingIndexVersion, cancellationToken).ConfigureAwait(false);
        }

        if (activeIsCurrent)
        {
            // Fixes the hard-coded "fts5-v1" target (K143): embed into the active generation.
            return await _generation
                .GenerateNextBatchAsync(maxChunks, state.ActiveIndexVersion, cancellationToken)
                .ConfigureAwait(false);
        }

        // The provider or model changed: regenerate side by side; searches keep the old one.
        return Map(await _rebuild.RunNextBatchAsync(maxChunks, cancellationToken).ConfigureAwait(false));
    }

    private async Task DiscardStagingAsync(string stagingVersion, CancellationToken cancellationToken)
    {
        await _lifecycle.AbandonAsync(stagingVersion, cancellationToken).ConfigureAwait(false);
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        await lease.Context.EmbeddingVectors
            .Where(vector => vector.IndexVersion == stagingVersion)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static EmbeddingGenerationBatchResult Map(EmbeddingIndexRebuildResult step) =>
        new(
            step.ChunksAttempted,
            step.ChunksEmbedded,
            step.ChunksFailed,
            ChunksSkipped: 0,
            step.ProviderUnavailable,
            ProviderChanged: step.ProviderChanged);

    private async ValueTask<ContextLease> CreateLeaseAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is null)
        {
            return new ContextLease(_context!, ownsContext: false);
        }

        CatalogueDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return new ContextLease(context, ownsContext: true);
    }

    private readonly struct ContextLease(CatalogueDbContext context, bool ownsContext) : IDisposable
    {
        public CatalogueDbContext Context { get; } = context;

        public void Dispose()
        {
            if (ownsContext)
            {
                Context.Dispose();
            }
        }
    }
}
