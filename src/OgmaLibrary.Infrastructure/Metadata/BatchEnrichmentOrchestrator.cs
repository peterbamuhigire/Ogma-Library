using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Metadata;

/// <summary>
/// Creates idempotent enrichment background-job rows for each requested book,
/// ready to be dequeued by the <c>EnrichmentWorker</c> (FR-META-006, NFR-OGMA-009).
/// </summary>
public sealed class BatchEnrichmentOrchestrator : IBatchEnrichmentOrchestrator
{
    /// <summary>Recoverable chunk size for large batch enrichment runs.</summary>
    public const int ChunkSize = 50;

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>
    /// Initializes a new instance of <see cref="BatchEnrichmentOrchestrator"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    internal BatchEnrichmentOrchestrator(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="BatchEnrichmentOrchestrator"/>.
    /// </summary>
    public BatchEnrichmentOrchestrator(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<int> StartAsync(
        IReadOnlyList<string> bookIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bookIds);

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        string batchId = Guid.NewGuid().ToString("N");
        List<PendingEnrichmentJob> requestedJobs = bookIds
            .Select((bookId, index) => new { BookId = bookId, Index = index })
            .Where(item => !string.IsNullOrWhiteSpace(item.BookId))
            .Select(item => new PendingEnrichmentJob(
                item.BookId,
                item.Index,
                ComputeIdempotencyKey(item.BookId)))
            .ToList();
        HashSet<string> scheduledKeys = await LoadExistingIdempotencyKeysAsync(
                context,
                requestedJobs.Select(job => job.IdempotencyKey),
                cancellationToken)
            .ConfigureAwait(false);

        int created = 0;
        foreach (PendingEnrichmentJob requestedJob in requestedJobs)
        {
            if (!scheduledKeys.Add(requestedJob.IdempotencyKey))
            {
                continue;
            }

            context.Jobs.Add(new JobRow
            {
                JobType = "Enrich",
                IdempotencyKey = requestedJob.IdempotencyKey,
                Status = 0, // Pending
                BookId = requestedJob.BookId,
                Payload = JsonSerializer.Serialize(new BatchEnrichmentJobPayload(
                    batchId,
                    ChunkIndex: requestedJob.Index / ChunkSize,
                    ChunkSize: Math.Min(ChunkSize, bookIds.Count - (requestedJob.Index / ChunkSize * ChunkSize)),
                    OrdinalInChunk: requestedJob.Index % ChunkSize)),
            });

            created++;
        }

        if (created > 0)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return created;
    }

    private static async Task<HashSet<string>> LoadExistingIdempotencyKeysAsync(
        CatalogueDbContext context,
        IEnumerable<string> idempotencyKeys,
        CancellationToken cancellationToken)
    {
        var existingKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (string[] chunk in idempotencyKeys.Distinct(StringComparer.Ordinal).Chunk(500))
        {
            List<string> matchedKeys = await context.Jobs
                .AsNoTracking()
                .Where(job => job.JobType == "Enrich" && chunk.Contains(job.IdempotencyKey))
                .Select(job => job.IdempotencyKey)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            existingKeys.UnionWith(matchedKeys);
        }

        return existingKeys;
    }

    private static string ComputeIdempotencyKey(string bookId)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"Enrich:{bookId}"));
        return Convert.ToHexStringLower(hash);
    }

    private sealed record PendingEnrichmentJob(string BookId, int Index, string IdempotencyKey);
}
