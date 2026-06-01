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

        int created = 0;
        string batchId = Guid.NewGuid().ToString("N");

        foreach ((string bookId, int index) in bookIds.Select((bookId, index) => (bookId, index)))
        {
            if (string.IsNullOrWhiteSpace(bookId))
            {
                continue;
            }

            string idempotencyKey = ComputeIdempotencyKey(bookId);

            // Skip if an enrichment job already exists for this book.
            bool exists = await context.Jobs
                .AsNoTracking()
                .AnyAsync(
                    j => j.IdempotencyKey == idempotencyKey && j.JobType == "Enrich",
                    cancellationToken)
                .ConfigureAwait(false);

            if (exists)
            {
                continue;
            }

            context.Jobs.Add(new JobRow
            {
                JobType = "Enrich",
                IdempotencyKey = idempotencyKey,
                Status = 0, // Pending
                BookId = bookId,
                Payload = JsonSerializer.Serialize(new BatchEnrichmentJobPayload(
                    batchId,
                    ChunkIndex: index / ChunkSize,
                    ChunkSize: Math.Min(ChunkSize, bookIds.Count - (index / ChunkSize * ChunkSize)),
                    OrdinalInChunk: index % ChunkSize)),
            });

            created++;
        }

        if (created > 0)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return created;
    }

    private static string ComputeIdempotencyKey(string bookId)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"Enrich:{bookId}"));
        return Convert.ToHexStringLower(hash);
    }
}
