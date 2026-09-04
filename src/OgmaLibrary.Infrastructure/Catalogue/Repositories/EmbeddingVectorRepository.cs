using System.Security.Cryptography;
using System.Text;
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
        if (vector.Vector.Length > 4096 || vector.Vector.Any(value => !float.IsFinite(value)))
        {
            throw new ArgumentException("Embedding vector dimensions or values are invalid.", nameof(vector));
        }
        if (vector.SourceHash.Length > 64 ||
            vector.SourceHash.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("Embedding source hash must be hexadecimal and at most 64 characters.", nameof(vector));
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        int[] existingDimensions = await context.EmbeddingVectors
            .AsNoTracking()
            .Where(row => row.ModelName == vector.ModelName &&
                         row.ModelVersion == vector.ModelVersion &&
                         row.ProviderKey == vector.ProviderKey)
            .Select(row => row.DimensionCount)
            .Distinct()
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (existingDimensions.Any(dimension => dimension != vector.Vector.Length))
        {
            throw new InvalidOperationException(
                $"Embedding dimension {vector.Vector.Length} does not match the existing " +
                $"model dimension for '{vector.ModelName}/{vector.ModelVersion}/{vector.ProviderKey}'.");
        }

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
        existing.SourceHash = vector.SourceHash.ToLowerInvariant();
        existing.ExtractorVersion = string.IsNullOrWhiteSpace(vector.ExtractorVersion)
            ? "unknown"
            : vector.ExtractorVersion;
        existing.ChunkerVersion = string.IsNullOrWhiteSpace(vector.ChunkerVersion)
            ? SearchChunker.CurrentVersion
            : vector.ChunkerVersion;
        existing.IndexVersion = string.IsNullOrWhiteSpace(vector.IndexVersion)
            ? "fts5-v1"
            : vector.IndexVersion;
        existing.ProviderKey = string.IsNullOrWhiteSpace(vector.ProviderKey)
            ? "ollama"
            : vector.ProviderKey;
        existing.IsTombstoned = false;
        existing.TombstonedUtc = null;

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
                vector.ModelVersion == modelVersion &&
                !vector.IsTombstoned,
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
            .Where(vector => !vector.IsTombstoned &&
                             vector.Chunk != null &&
                             vector.Chunk.BookId == bookId)
            .OrderBy(vector => vector.ChunkId)
            .ThenBy(vector => vector.ModelName)
            .ThenBy(vector => vector.ModelVersion)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(MapToRecord).ToList();
    }

    /// <inheritdoc />
    public async Task<int> GetStaleCountAsync(
        string bookId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        List<EmbeddingVectorRow> rows = await lease.Context.EmbeddingVectors
            .AsNoTracking()
            .Include(vector => vector.Chunk)
            .Where(vector => !vector.IsTombstoned &&
                             vector.Chunk != null &&
                             vector.Chunk.BookId == bookId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Count(vector => vector.Chunk is not null &&
            !string.Equals(
                vector.SourceHash,
                ComputeSourceHash(vector.Chunk, vector.Chunk.ChunkText ?? string.Empty),
                StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<int> TombstoneStaleAsync(string bookId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        List<EmbeddingVectorRow> rows = await lease.Context.EmbeddingVectors
            .Include(vector => vector.Chunk)
            .Where(vector => !vector.IsTombstoned &&
                             vector.Chunk != null &&
                             vector.Chunk.BookId == bookId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<EmbeddingVectorRow> stale = rows
            .Where(vector => vector.Chunk is not null &&
                !string.Equals(
                    vector.SourceHash,
                    ComputeSourceHash(vector.Chunk, vector.Chunk.ChunkText ?? string.Empty),
                    StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (EmbeddingVectorRow row in stale)
        {
            row.IsTombstoned = true;
            row.TombstonedUtc = now;
        }

        if (stale.Count > 0)
        {
            await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return stale.Count;
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

    private static string ComputeSourceHash(SearchChunkRow chunk, string text)
    {
        byte[] data = Encoding.UTF8.GetBytes(
            $"{chunk.BookId}|{chunk.ChunkId.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{chunk.IndexVersion}|{chunk.ExtractionArtifactId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"}|{text}");
        return Convert.ToHexStringLower(SHA256.HashData(data));
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
            row.GeneratedAtUtc,
            row.SourceHash,
            row.ExtractorVersion,
            row.ChunkerVersion,
            row.IndexVersion,
            row.ProviderKey,
            row.IsTombstoned,
            row.TombstonedUtc);

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
