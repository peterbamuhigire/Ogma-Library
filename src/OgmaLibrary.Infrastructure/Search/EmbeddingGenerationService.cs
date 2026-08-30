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
/// calls the local Ollama embedding provider, and persists model-versioned
/// vectors as a derived index.
/// </summary>
public sealed class EmbeddingGenerationService : IEmbeddingGenerationService, ISemanticSearchReadModel
{
    internal const string DefaultModelName = "nomic-embed-text";
    internal const string DefaultModelVersion = "nomic-embed-text:latest";
    internal const string DefaultProviderKey = "ollama";

    private const int ActiveBookStatus = 0;
    private const int JobFailed = 3;

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly IOllamaEmbeddingProvider _provider;
    private readonly IEmbeddingVectorRepository _vectors;
    private readonly ObservableEvents<SemanticIndexEvent> _events = new();

    /// <summary>
    /// Initializes a new instance of <see cref="EmbeddingGenerationService"/>.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public EmbeddingGenerationService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IOllamaEmbeddingProvider provider,
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
        IOllamaEmbeddingProvider provider,
        IEmbeddingVectorRepository vectors)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(vectors);

        _context = context;
        _provider = provider;
        _vectors = vectors;
    }

    /// <inheritdoc />
    public IObservable<SemanticIndexEvent> Events => _events;

    /// <inheritdoc />
    public async Task<EmbeddingGenerationBatchResult> GenerateNextBatchAsync(
        int maxChunks,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxChunks);

        if (!_provider.IsLocalOnly ||
            !await _provider.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            _events.Publish(new SemanticIndexEvent.OllamaUnavailable(DateTimeOffset.UtcNow));
            return new EmbeddingGenerationBatchResult(0, 0, 0, 0, ProviderUnavailable: true);
        }

        IReadOnlyList<PendingChunk> chunks = await FindPendingChunksAsync(maxChunks, cancellationToken)
            .ConfigureAwait(false);
        int embedded = 0;
        int failed = 0;

        foreach (PendingChunk chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SetBookEmbeddingStatusAsync(
                    chunk.BookId,
                    SearchEmbeddingStatus.Embedding,
                    cancellationToken)
                .ConfigureAwait(false);

            try
            {
                OllamaEmbeddingResult result = await _provider
                    .EmbedAsync(chunk.Text, DefaultModelName, cancellationToken)
                    .ConfigureAwait(false);
                ValidateResult(result);

                await _vectors.CreateAsync(
                        new EmbeddingVectorRecord(
                            Id: 0,
                            ChunkId: chunk.ChunkId,
                            ModelName: DefaultModelName,
                            ModelVersion: string.IsNullOrWhiteSpace(result.ModelVersion)
                                ? DefaultModelVersion
                                : result.ModelVersion,
                            Vector: result.Vector,
                            DimensionCount: result.Vector.Length,
                            GeneratedAtUtc: DateTimeOffset.UtcNow,
                            SourceHash: chunk.SourceHash,
                            ExtractorVersion: chunk.ExtractorVersion,
                            ChunkerVersion: SearchChunker.CurrentVersion,
                            IndexVersion: chunk.IndexVersion,
                            ProviderKey: _provider.ProviderKey),
                        cancellationToken)
                    .ConfigureAwait(false);

                embedded++;
                await PublishProgressAsync(chunk, cancellationToken).ConfigureAwait(false);
                await RefreshBookEmbeddingStatusAsync(chunk.BookId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                await RecordFailureAsync(chunk, ex.Message, cancellationToken).ConfigureAwait(false);
                await SetBookEmbeddingStatusAsync(
                        chunk.BookId,
                        SearchEmbeddingStatus.Failed,
                        cancellationToken)
                    .ConfigureAwait(false);
                _events.Publish(new SemanticIndexEvent.EmbeddingFailed(
                    chunk.ChunkId,
                    chunk.BookId,
                    TrimError(ex.Message)));
            }
        }

        return new EmbeddingGenerationBatchResult(
            ChunksAttempted: chunks.Count,
            ChunksEmbedded: embedded,
            ChunksFailed: failed,
            ChunksSkipped: 0,
            ProviderUnavailable: false);
    }

    private async Task<IReadOnlyList<PendingChunk>> FindPendingChunksAsync(
        int maxChunks,
        CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

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
                    vector.ModelName == DefaultModelName &&
                    vector.ModelVersion == DefaultModelVersion &&
                    vector.ProviderKey == DefaultProviderKey &&
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
                chunk.IndexVersion,
                chunk.ExtractionArtifactId))
            .Take(maxChunks)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task PublishProgressAsync(PendingChunk chunk, CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        int totalChunks = await context.SearchChunks
            .AsNoTracking()
            .CountAsync(searchChunk => searchChunk.BookId == chunk.BookId, cancellationToken)
            .ConfigureAwait(false);
        int totalEmbedded = await context.EmbeddingVectors
            .AsNoTracking()
            .CountAsync(vector =>
                vector.Chunk != null &&
                vector.Chunk.BookId == chunk.BookId &&
                vector.ModelName == DefaultModelName &&
                vector.ModelVersion == DefaultModelVersion &&
                vector.ProviderKey == DefaultProviderKey &&
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

        int totalChunks = await context.SearchChunks
            .CountAsync(chunk => chunk.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);
        int totalEmbedded = await context.EmbeddingVectors
            .CountAsync(vector =>
                vector.Chunk != null &&
                vector.Chunk.BookId == bookId &&
                vector.ModelName == DefaultModelName &&
                vector.ModelVersion == DefaultModelVersion &&
                vector.ProviderKey == DefaultProviderKey &&
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
        string key = ComputeIdempotencyKey(chunk.BookId, chunk.ChunkId);
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

    private static string TrimError(string message) =>
        message.Length <= 4096 ? message : message[..4096];

    private static string ComputeIdempotencyKey(string bookId, long chunkId)
    {
        byte[] data = Encoding.UTF8.GetBytes(
            $"{bookId}|EmbeddingFailed|{chunkId.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{DefaultModelName}|{DefaultModelVersion}");
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

    private static void ValidateResult(OllamaEmbeddingResult result)
    {
        if (!string.Equals(result.ModelName, DefaultModelName, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(result.ModelVersion) ||
            result.Vector.Length == 0 ||
            result.Vector.Length > 4096 ||
            result.Vector.Any(value => !float.IsFinite(value)))
        {
            throw new InvalidOperationException("Embedding provider returned an invalid model or vector.");
        }
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
