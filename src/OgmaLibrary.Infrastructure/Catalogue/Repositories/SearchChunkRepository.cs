using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ISearchChunkRepository"/> for Phase 10
/// search chunks and FTS5 trigger-backed indexing.
/// </summary>
public sealed class SearchChunkRepository : ISearchChunkRepository
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>
    /// Initializes a new instance of <see cref="SearchChunkRepository"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    internal SearchChunkRepository(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="SearchChunkRepository"/>.
    /// </summary>
    /// <param name="contextFactory">The catalogue DB context factory.</param>
    [ActivatorUtilitiesConstructor]
    public SearchChunkRepository(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchChunkRecord>> ReplaceForBookAsync(
        string bookId,
        SearchChunkSource source,
        IReadOnlyList<SearchChunkRecord> chunks,
        CancellationToken cancellationToken,
        string? indexVersion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(chunks);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        var tx = await context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (tx.ConfigureAwait(false))
        {
            List<SearchChunkRow> existing = await context.SearchChunks
                .Where(c => c.BookId == bookId &&
                            c.Source == (int)source &&
                            (indexVersion == null || c.IndexVersion == indexVersion))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            context.SearchChunks.RemoveRange(existing);

            var rows = chunks
                .OrderBy(c => c.ChunkIndex)
                .Select(chunk => MapToRow(bookId, chunk, source))
                .ToList();

            context.SearchChunks.AddRange(rows);

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            return rows.Select(row => MapToRecord(row, pageIndex: null)).ToList();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchChunkRecord>> ListForBookAsync(
        string bookId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        List<SearchChunkRow> rows = await context.SearchChunks
            .AsNoTracking()
            .Include(c => c.ExtractedPage)
            .Where(c => c.BookId == bookId)
            .OrderBy(c => c.Source)
            .ThenBy(c => c.ExtractedPage == null ? int.MaxValue : c.ExtractedPage.PageNumber)
            .ThenBy(c => c.ChunkIndex)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(row => MapToRecord(row, row.ExtractedPage?.PageNumber))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        return await context.SearchChunks.CountAsync(cancellationToken).ConfigureAwait(false);
    }

    private static SearchChunkRow MapToRow(string bookId, SearchChunkRecord chunk, SearchChunkSource source) =>
        new()
        {
            BookId = bookId,
            ExtractedPageId = chunk.ExtractedPageId,
            ChunkIndex = chunk.ChunkIndex,
            ChunkText = chunk.Text,
            Source = (int)source,
            TokenCount = chunk.TokenCount,
            CreatedAtUtc = chunk.CreatedAtUtc == default
                ? DateTimeOffset.UtcNow
                : chunk.CreatedAtUtc,
            ExtractionArtifactId = chunk.ExtractionArtifactId,
            IndexVersion = string.IsNullOrWhiteSpace(chunk.IndexVersion) ? "fts5-v1" : chunk.IndexVersion,
        };

    private static SearchChunkRecord MapToRecord(SearchChunkRow row, int? pageIndex) =>
        new(
            row.ChunkId,
            row.BookId,
            row.ExtractedPageId,
            pageIndex,
            row.ChunkIndex,
            row.ChunkText ?? string.Empty,
            row.TokenCount,
            (SearchChunkSource)row.Source,
            row.CreatedAtUtc,
            row.ExtractionArtifactId,
            row.IndexVersion);

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
