using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>
/// Transactional erasure service for locally derived semantic embedding data.
/// </summary>
public sealed class EmbeddingErasureService : IEmbeddingErasureService
{
    /// <summary>Audit event type written after successful embedding erasure.</summary>
    public const string AuditEventType = "EmbeddingVectorsErased";

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>Initializes a new instance of <see cref="EmbeddingErasureService"/>.</summary>
    [ActivatorUtilitiesConstructor]
    public EmbeddingErasureService(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    internal EmbeddingErasureService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<EmbeddingErasureResult> EraseAllAsync(CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset erasedAtUtc = DateTimeOffset.UtcNow;
        int vectorsErased = await context.EmbeddingVectors
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
        int deleted = await context.EmbeddingVectors
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        int booksReset = await context.Books
            .Where(book => book.EmbeddingStatus != (int)SearchEmbeddingStatus.NotEmbedded)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    book => book.EmbeddingStatus,
                    (int)SearchEmbeddingStatus.NotEmbedded),
                cancellationToken)
            .ConfigureAwait(false);

        context.AuditEvents.Add(new AuditEventRow
        {
            EventType = AuditEventType,
            EntityId = "all",
            EntityType = "EmbeddingVectors",
            Timestamp = erasedAtUtc,
            IsLocalOnly = true,
            AfterJson = $$"""{"vectorsErased":{{deleted.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"booksReset":{{booksReset.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"erasedAtUtc":"{{erasedAtUtc:O}}"}""",
        });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new EmbeddingErasureResult(vectorsErased, booksReset, erasedAtUtc);
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
