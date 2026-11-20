using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>
/// Phase 11 embedding-generation pipeline. It consumes Phase 10 search chunks,
/// calls the configured local embedding provider, and persists model-versioned
/// vectors as a derived index.
/// </summary>
public sealed class EmbeddingGenerationService : IEmbeddingGenerationService, IStagedEmbeddingGenerationService, ISemanticSearchReadModel
{
    private const int ActiveBookStatus = 0;
    private const int JobFailed = 3;

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly IEmbeddingProvider _provider;
    private readonly IEmbeddingVectorRepository _vectors;
    private readonly ObservableEvents<SemanticIndexEvent> _events = new();
    private readonly Queue<DateTimeOffset> _recentEmbeddings = new();
    private readonly Lock _rateGate = new();

    /// <summary>
    /// Initializes a new instance of <see cref="EmbeddingGenerationService"/>.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public EmbeddingGenerationService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IEmbeddingProvider provider,
        IEmbeddingVectorRepository vectors)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(vectors);

        _contextFactory = contextFactory;
        _provider = provider;
        _vectors = vectors;
    }

    internal EmbeddingGenerationService(
        CatalogueDbContext context,
        IEmbeddingProvider provider,
        IEmbeddingVectorRepository vectors)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(vectors);

        _context = context;
        _provider = provider;
        _vectors = vectors;
    }

    /// <summary>How far back the rolling embedding rate for the progress estimate looks.</summary>
    internal static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(2);

    /// <inheritdoc />
    public IObservable<SemanticIndexEvent> Events => _events;

    /// <summary>Clock for the rolling rate; tests substitute it.</summary>
    internal TimeProvider Clock { get; init; } = TimeProvider.System;

    /// <inheritdoc />
    public async Task<EmbeddingGenerationBatchResult> GenerateNextBatchAsync(
        int maxChunks,
        CancellationToken cancellationToken)
        => await GenerateNextBatchAsync(maxChunks, "fts5-v1", cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<EmbeddingGenerationBatchResult> GenerateNextBatchAsync(
        int maxChunks,
        string indexVersion,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxChunks);
        ArgumentException.ThrowIfNullOrWhiteSpace(indexVersion);
        if (indexVersion.Length > 128 || indexVersion.Any(char.IsControl) || indexVersion.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Semantic index version must be a bounded token.", nameof(indexVersion));
        }

        if (!_provider.IsLocalOnly ||
            !await _provider.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            _events.Publish(new SemanticIndexEvent.OllamaUnavailable(DateTimeOffset.UtcNow));
            return new EmbeddingGenerationBatchResult(0, 0, 0, 0, ProviderUnavailable: true);
        }

        // The identity is fixed for the whole batch: if the provider or model changes mid-batch
        // the batch stops rather than write a vector of another model into this generation.
        ModelIdentity identity = ModelIdentity.Of(_provider);
        IReadOnlyList<PendingChunk> chunks = await FindPendingChunksAsync(maxChunks, indexVersion, identity, cancellationToken)
            .ConfigureAwait(false);
        int attempted = 0;
        int embedded = 0;
        int failed = 0;
        int inputTokenCount = 0;
        bool providerChanged = false;

        foreach (PendingChunk chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ModelIdentity.Of(_provider) != identity)
            {
                providerChanged = true;
                break;
            }

            attempted++;
            inputTokenCount = checked(inputTokenCount + SearchChunker.CountTokens(chunk.Text));
            await SetBookEmbeddingStatusAsync(
                    chunk.BookId,
                    SearchEmbeddingStatus.Embedding,
                    cancellationToken)
                .ConfigureAwait(false);

            try
            {
                EmbeddingResult result = await _provider
                    .EmbedAsync(chunk.Text, EmbeddingInputKind.Passage, cancellationToken)
                    .ConfigureAwait(false);
                if (!identity.Matches(result) && ModelIdentity.Of(_provider) != identity)
                {
                    // The provider switched while this chunk was embedding: discard the vector.
                    providerChanged = true;
                    await RefreshBookEmbeddingStatusAsync(chunk.BookId, indexVersion, cancellationToken).ConfigureAwait(false);
                    break;
                }

                ValidateResult(result, identity);

                await _vectors.CreateAsync(
                        new EmbeddingVectorRecord(
                            Id: 0,
                            ChunkId: chunk.ChunkId,
                            ModelName: identity.ModelId,
                            ModelVersion: identity.ModelVersion,
                            Vector: result.Vector,
                            DimensionCount: result.Vector.Length,
                            GeneratedAtUtc: DateTimeOffset.UtcNow,
                            SourceHash: chunk.SourceHash,
                            ExtractorVersion: chunk.ExtractorVersion,
                            ChunkerVersion: SearchChunker.CurrentVersion,
                            IndexVersion: chunk.IndexVersion,
                            ProviderKey: identity.ProviderKey),
                        cancellationToken)
                    .ConfigureAwait(false);

                embedded++;
                RecordEmbedding();
                await PublishProgressAsync(chunk, cancellationToken).ConfigureAwait(false);
                await RefreshBookEmbeddingStatusAsync(chunk.BookId, indexVersion, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                const string message = "Embedding generation failed.";
                await RecordFailureAsync(chunk, message, cancellationToken).ConfigureAwait(false);
                await SetBookEmbeddingStatusAsync(
                        chunk.BookId,
                        SearchEmbeddingStatus.Failed,
                        cancellationToken)
                    .ConfigureAwait(false);
                _events.Publish(new SemanticIndexEvent.EmbeddingFailed(
                    chunk.ChunkId,
                    chunk.BookId,
                    message));
            }
        }

        return new EmbeddingGenerationBatchResult(
            ChunksAttempted: attempted,
            ChunksEmbedded: embedded,
            ChunksFailed: failed,
            ChunksSkipped: 0,
            ProviderUnavailable: false,
            InputTokenCount: inputTokenCount,
            ExternalProviderEgressBytes: 0,
            EstimatedExternalProviderCostUsd: 0m,
            ProviderChanged: providerChanged);
    }

    /// <inheritdoc />
    public async Task<SemanticIndexProgress> GetProgressAsync(CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        EmbeddingIndexStateRow? state = await context.EmbeddingIndexState
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.StateKey == "semantic", cancellationToken)
            .ConfigureAwait(false);
        string targetVersion = state?.StagingIndexVersion ?? state?.ActiveIndexVersion ?? "fts5-v1";
        (string providerKey, string modelId, string modelVersion) = ProviderIdentity();

        IQueryable<SearchChunkRow> eligible = context.SearchChunks
            .AsNoTracking()
            .Where(chunk =>
                chunk.ChunkText != null &&
                chunk.ChunkText != string.Empty &&
                context.Books.Any(book =>
                    book.BookId == chunk.BookId &&
                    book.Status == ActiveBookStatus &&
                    book.IndexStatus == (int)SearchBookIndexStatus.Indexed));
        IQueryable<SearchChunkRow> waiting = eligible.Where(chunk =>
            !context.EmbeddingVectors.Any(vector =>
                vector.ChunkId == chunk.ChunkId &&
                !vector.IsTombstoned &&
                vector.ModelName == modelId &&
                vector.ModelVersion == modelVersion &&
                vector.ProviderKey == providerKey &&
                vector.IndexVersion == targetVersion &&
                vector.SourceHash.Length > 0));

        int total = await eligible.CountAsync(cancellationToken).ConfigureAwait(false);
        int remaining = await waiting.CountAsync(cancellationToken).ConfigureAwait(false);
        int booksWaiting = await waiting.Select(chunk => chunk.BookId).Distinct().CountAsync(cancellationToken)
            .ConfigureAwait(false);
        int booksTotal = await eligible.Select(chunk => chunk.BookId).Distinct().CountAsync(cancellationToken)
            .ConfigureAwait(false);

        return new SemanticIndexProgress(
            ChunksEmbedded: total - remaining,
            ChunksTotal: total,
            BooksWaiting: booksWaiting,
            EstimatedRemaining: EstimateRemaining(remaining),
            IsRegenerating: state?.StagingIndexVersion is not null,
            BooksTotal: booksTotal);
    }

    private void RecordEmbedding()
    {
        DateTimeOffset now = Clock.GetUtcNow();
        lock (_rateGate)
        {
            _recentEmbeddings.Enqueue(now);
            TrimRateWindow(now);
        }
    }

    private TimeSpan? EstimateRemaining(int remaining)
    {
        if (remaining == 0)
        {
            return TimeSpan.Zero;
        }

        DateTimeOffset now = Clock.GetUtcNow();
        lock (_rateGate)
        {
            TrimRateWindow(now);
            if (_recentEmbeddings.Count < 2)
            {
                return null;
            }

            TimeSpan span = now - _recentEmbeddings.Peek();
            if (span <= TimeSpan.Zero)
            {
                return null;
            }

            double perSecond = _recentEmbeddings.Count / span.TotalSeconds;
            return TimeSpan.FromSeconds(Math.Ceiling(remaining / perSecond));
        }
    }

    private void TrimRateWindow(DateTimeOffset now)
    {
        while (_recentEmbeddings.Count > 0 && now - _recentEmbeddings.Peek() > RateWindow)
        {
            _recentEmbeddings.Dequeue();
        }
    }

    private async Task<IReadOnlyList<PendingChunk>> FindPendingChunksAsync(
        int maxChunks,
        string indexVersion,
        ModelIdentity identity,
        CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        (string providerKey, string modelId, string modelVersion) = (identity.ProviderKey, identity.ModelId, identity.ModelVersion);

        return await context.SearchChunks
            .AsNoTracking()
            .Where(chunk =>
                chunk.ChunkText != null &&
                chunk.ChunkText != string.Empty &&
                context.Books.Any(book =>
                    book.BookId == chunk.BookId &&
                    book.Status == ActiveBookStatus &&
                    book.IndexStatus == (int)SearchBookIndexStatus.Indexed) &&
                !context.EmbeddingVectors.Any(vector =>
                    vector.ChunkId == chunk.ChunkId &&
                    !vector.IsTombstoned &&
                    vector.ModelName == modelId &&
                    vector.ModelVersion == modelVersion &&
                    vector.ProviderKey == providerKey &&
                    vector.IndexVersion == indexVersion &&
                    vector.SourceHash.Length > 0))
            .OrderBy(chunk => chunk.ChunkId)
            .Select(chunk => new PendingChunk(
                chunk.ChunkId,
                chunk.BookId,
                chunk.ChunkText!,
                ComputeSourceHash(
                    chunk.BookId,
                    chunk.ChunkId,
                    chunk.ChunkText!,
                    chunk.IndexVersion,
                    chunk.ExtractionArtifactId),
                chunk.ExtractedPage == null ? "metadata-v1" : chunk.ExtractedPage.ExtractorVersion,
                indexVersion,
                chunk.ExtractionArtifactId))
            .Take(maxChunks)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task PublishProgressAsync(PendingChunk chunk, CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        (string providerKey, string modelId, string modelVersion) = ProviderIdentity();

        int totalChunks = await context.SearchChunks
            .AsNoTracking()
            .CountAsync(searchChunk => searchChunk.BookId == chunk.BookId, cancellationToken)
            .ConfigureAwait(false);
        int totalEmbedded = await context.EmbeddingVectors
            .AsNoTracking()
            .CountAsync(vector =>
                vector.Chunk != null &&
                vector.Chunk.BookId == chunk.BookId &&
                !vector.IsTombstoned &&
                vector.ModelName == modelId &&
                vector.ModelVersion == modelVersion &&
                vector.ProviderKey == providerKey &&
                vector.IndexVersion == chunk.IndexVersion &&
                vector.SourceHash.Length > 0,
                cancellationToken)
            .ConfigureAwait(false);

        _events.Publish(new SemanticIndexEvent.EmbeddingGenerated(
            chunk.ChunkId,
            chunk.BookId,
            totalEmbedded,
            totalChunks));
    }

    private async Task RefreshBookEmbeddingStatusAsync(
        string bookId,
        string indexVersion,
        CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        BookRow? book = await context.Books
            .FirstOrDefaultAsync(row => row.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);
        if (book is null)
        {
            return;
        }

        (string providerKey, string modelId, string modelVersion) = ProviderIdentity();
        int totalChunks = await context.SearchChunks
            .CountAsync(chunk => chunk.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);
        int totalEmbedded = await context.EmbeddingVectors
            .CountAsync(vector =>
                vector.Chunk != null &&
                vector.Chunk.BookId == bookId &&
                !vector.IsTombstoned &&
                vector.ModelName == modelId &&
                vector.ModelVersion == modelVersion &&
                vector.ProviderKey == providerKey &&
                vector.IndexVersion == indexVersion &&
                vector.SourceHash.Length > 0,
                cancellationToken)
            .ConfigureAwait(false);

        book.EmbeddingStatus = totalChunks > 0 && totalEmbedded >= totalChunks
            ? (int)SearchEmbeddingStatus.Embedded
            : (int)SearchEmbeddingStatus.Embedding;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SetBookEmbeddingStatusAsync(
        string bookId,
        SearchEmbeddingStatus status,
        CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        BookRow? book = await context.Books
            .FirstOrDefaultAsync(row => row.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);
        if (book is null)
        {
            return;
        }

        book.EmbeddingStatus = (int)status;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RecordFailureAsync(
        PendingChunk chunk,
        string message,
        CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        string key = ComputeIdempotencyKey(chunk.BookId, chunk.ChunkId, chunk.IndexVersion);
        JobRow? job = await context.Jobs
            .FirstOrDefaultAsync(row => row.IdempotencyKey == key, cancellationToken)
            .ConfigureAwait(false);
        string payload = $$"""{"source":"semantic-embedding","chunkId":{{chunk.ChunkId.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}""";

        if (job is null)
        {
            context.Jobs.Add(new JobRow
            {
                JobType = "EmbeddingFailed",
                IdempotencyKey = key,
                Status = JobFailed,
                BookId = chunk.BookId,
                Payload = payload,
                ErrorMessage = TrimError(message),
                CompletedUtc = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            job.Status = JobFailed;
            job.Payload = payload;
            job.ErrorMessage = TrimError(message);
            job.CompletedUtc = DateTimeOffset.UtcNow;
            job.RetryCount += 1;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ContextLease> CreateLeaseAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is null)
        {
            return new ContextLease(_context!, ownsContext: false);
        }

        CatalogueDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        return new ContextLease(context, ownsContext: true);
    }

    private (string ProviderKey, string ModelId, string ModelVersion) ProviderIdentity() =>
        (_provider.ProviderKey, _provider.ModelId, _provider.ModelVersion);

    private static string TrimError(string message) =>
        message.Length <= 4096 ? message : message[..4096];

    private string ComputeIdempotencyKey(string bookId, long chunkId, string indexVersion)
    {
        byte[] data = Encoding.UTF8.GetBytes(
            $"{bookId}|EmbeddingFailed|{chunkId.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{_provider.ModelId}|{_provider.ModelVersion}|{indexVersion}");
        return Convert.ToHexStringLower(SHA256.HashData(data))[..32];
    }

    private static string ComputeSourceHash(
        string bookId,
        long chunkId,
        string text,
        string indexVersion,
        long? extractionArtifactId)
    {
        byte[] data = Encoding.UTF8.GetBytes(
            $"{bookId}|{chunkId.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{indexVersion}|{extractionArtifactId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"}|{text}");
        return Convert.ToHexStringLower(SHA256.HashData(data));
    }

    private static void ValidateResult(EmbeddingResult result, ModelIdentity identity)
    {
        if (!identity.Matches(result) ||
            result.Vector.Length != identity.Dimensions ||
            result.Vector.Length == 0 ||
            result.Vector.Length > 4096 ||
            result.Vector.Any(value => !float.IsFinite(value)))
        {
            throw new InvalidOperationException("Embedding provider returned an invalid model or vector.");
        }
    }

    private readonly record struct ModelIdentity(string ProviderKey, string ModelId, string ModelVersion, int Dimensions)
    {
        public static ModelIdentity Of(IEmbeddingProvider provider) =>
            new(provider.ProviderKey, provider.ModelId, provider.ModelVersion, provider.Dimensions);

        public bool Matches(EmbeddingResult result) =>
            string.Equals(result.ProviderKey, ProviderKey, StringComparison.Ordinal) &&
            string.Equals(result.ModelId, ModelId, StringComparison.Ordinal) &&
            string.Equals(result.ModelVersion, ModelVersion, StringComparison.Ordinal);
    }

    private sealed record PendingChunk(
        long ChunkId,
        string BookId,
        string Text,
        string SourceHash,
        string ExtractorVersion,
        string IndexVersion,
        long? ExtractionArtifactId);

    private sealed class ObservableEvents<TEvent> : IObservable<TEvent>
    {
        private readonly object _gate = new();
        private readonly List<IObserver<TEvent>> _observers = [];

        public IDisposable Subscribe(IObserver<TEvent> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            lock (_gate)
            {
                _observers.Add(observer);
            }

            return new Subscription(this, observer);
        }

        public void Publish(TEvent update)
        {
            IObserver<TEvent>[] observers;
            lock (_gate)
            {
                observers = _observers.ToArray();
            }

            foreach (IObserver<TEvent> observer in observers)
            {
                observer.OnNext(update);
            }
        }

        private void Unsubscribe(IObserver<TEvent> observer)
        {
            lock (_gate)
            {
                _observers.Remove(observer);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly ObservableEvents<TEvent> _owner;
            private IObserver<TEvent>? _observer;

            public Subscription(ObservableEvents<TEvent> owner, IObserver<TEvent> observer)
            {
                _owner = owner;
                _observer = observer;
            }

            public void Dispose()
            {
                IObserver<TEvent>? observer = Interlocked.Exchange(ref _observer, null);
                if (observer is not null)
                {
                    _owner.Unsubscribe(observer);
                }
            }
        }
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
