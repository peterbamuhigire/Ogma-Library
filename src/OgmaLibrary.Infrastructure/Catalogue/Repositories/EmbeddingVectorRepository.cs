using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Repositories;

/// <summary>
/// EF Core repository for Phase 11 embedding vectors stored as little-endian
/// float BLOBs in SQLite.
/// </summary>
public sealed class EmbeddingVectorRepository : IEmbeddingVectorRepository
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    internal EmbeddingVectorRepository(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="EmbeddingVectorRepository"/>.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public EmbeddingVectorRepository(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<EmbeddingVectorRecord> CreateAsync(
        EmbeddingVectorRecord vector,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vector);
        ArgumentException.ThrowIfNullOrWhiteSpace(vector.ModelName);
        ArgumentException.ThrowIfNullOrWhiteSpace(vector.ModelVersion);
        if (vector.Vector.Length == 0)
        {
            throw new ArgumentException("Embedding vector must contain at least one dimension.", nameof(vector));
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        EmbeddingVectorRow? existing = await context.EmbeddingVectors
            .SingleOrDefaultAsync(row =>
                row.ChunkId == vector.ChunkId &&
                row.ModelName == vector.ModelName &&
                row.ModelVersion == vector.ModelVersion,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            existing = new EmbeddingVectorRow
            {
                ChunkId = vector.ChunkId,
                ModelName = vector.ModelName,
                ModelVersion = vector.ModelVersion,
            };
            context.EmbeddingVectors.Add(existing);
        }

        existing.DimensionCount = vector.Vector.Length;
        existing.VectorBlob = Serialize(vector.Vector);
        existing.GeneratedAtUtc = vector.GeneratedAtUtc == default
            ? DateTimeOffset.UtcNow
            : vector.GeneratedAtUtc;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapToRecord(existing);
    }

    /// <inheritdoc />
    public async Task<EmbeddingVectorRecord?> GetForChunkAsync(
        long chunkId,
        string modelName,
        string modelVersion,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelVersion);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        EmbeddingVectorRow? row = await context.EmbeddingVectors
            .AsNoTracking()
            .SingleOrDefaultAsync(vector =>
                vector.ChunkId == chunkId &&
                vector.ModelName == modelName &&
                vector.ModelVersion == modelVersion,
                cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : MapToRecord(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmbeddingVectorRecord>> GetAllForBookAsync(
        string bookId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        List<EmbeddingVectorRow> rows = await context.EmbeddingVectors
            .AsNoTracking()
            .Include(vector => vector.Chunk)
            .Where(vector => vector.Chunk != null && vector.Chunk.BookId == bookId)
            .OrderBy(vector => vector.ChunkId)
            .ThenBy(vector => vector.ModelName)
            .ThenBy(vector => vector.ModelVersion)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(MapToRecord).ToList();
    }

    /// <inheritdoc />
    public async Task<int> DeleteAllAsync(CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        return await context.EmbeddingVectors
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static byte[] Serialize(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] Deserialize(byte[]? bytes, int dimensionCount)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return [];
        }

        int count = Math.Min(dimensionCount, bytes.Length / sizeof(float));
        var vector = new float[count];
        Buffer.BlockCopy(bytes, 0, vector, 0, count * sizeof(float));
        return vector;
    }

    private static EmbeddingVectorRecord MapToRecord(EmbeddingVectorRow row) =>
        new(
            row.VectorId,
            row.ChunkId,
            row.ModelName,
            row.ModelVersion,
            Deserialize(row.VectorBlob, row.DimensionCount),
            row.DimensionCount,
            row.GeneratedAtUtc);

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
