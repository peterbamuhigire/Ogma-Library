using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IExtractedTextStore"/> for Phase 10
/// extracted-page persistence.
/// </summary>
public sealed class ExtractedTextStore : IExtractedTextStore
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>
    /// Initializes a new instance of <see cref="ExtractedTextStore"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    internal ExtractedTextStore(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ExtractedTextStore"/>.
    /// </summary>
    /// <param name="contextFactory">The catalogue DB context factory.</param>
    [ActivatorUtilitiesConstructor]
    public ExtractedTextStore(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<ExtractedPageRecord?> GetPageAsync(
        string bookId,
        int pageIndex,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        ExtractedPageRow? row = await context.ExtractedPages
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.BookId == bookId && p.PageNumber == pageIndex && p.Source == "Extraction",
                cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : MapToRecord(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExtractedPageRecord>> ListForBookAsync(
        string bookId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        List<ExtractedPageRow> rows = await context.ExtractedPages
            .AsNoTracking()
            .Where(p => p.BookId == bookId)
            .OrderBy(p => p.PageNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(MapToRecord).ToList();
    }

    /// <inheritdoc />
    public async Task<ExtractedPageRecord> UpsertPageAsync(
        ExtractedPageRecord page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(page.BookId);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        string source = NormalizeSource(page.Source);

        ExtractedPageRow? existing = await context.ExtractedPages
            .FirstOrDefaultAsync(
                p => p.BookId == page.BookId && p.PageNumber == page.PageIndex && p.Source == source,
                cancellationToken)
            .ConfigureAwait(false);

        Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues? originalValues = existing is null
            ? null
            : context.Entry(existing).OriginalValues.Clone();
        ExtractedPageRow? addedRow = null;

        var tx = await context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (tx.ConfigureAwait(false))
        {
            try
            {
                if (existing is null)
                {
                    addedRow = MapToRow(page);
                    context.ExtractedPages.Add(addedRow);
                    existing = addedRow;
                }
                else
                {
                    Apply(page, existing);
                }

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                if (addedRow is not null)
                {
                    context.Entry(addedRow).State = EntityState.Detached;
                }
                else if (existing is not null && originalValues is not null)
                {
                    context.Entry(existing).CurrentValues.SetValues(originalValues);
                    context.Entry(existing).State = EntityState.Unchanged;
                }

                throw;
            }
        }

        return MapToRecord(existing);
    }

    private static ExtractedPageRecord MapToRecord(ExtractedPageRow row) =>
        new(
            row.ExtractedPageId,
            row.BookId,
            row.PageNumber,
            row.TextContent,
            (SearchExtractionQuality)row.ExtractionQuality,
            row.WordCount,
            row.ContentHash,
            row.ExtractionUtc ?? DateTimeOffset.MinValue,
            row.Source,
            row.ExtractorVersion);

    private static ExtractedPageRow MapToRow(ExtractedPageRecord page)
    {
        var row = new ExtractedPageRow();
        Apply(page, row);
        return row;
    }

    private static void Apply(ExtractedPageRecord page, ExtractedPageRow row)
    {
        row.BookId = page.BookId;
        row.PageNumber = page.PageIndex;
        row.TextContent = page.Text;
        row.ExtractionQuality = (int)page.Quality;
        row.WordCount = page.WordCount;
        row.ContentHash = page.ContentHash;
        row.Source = NormalizeSource(page.Source);
        row.ExtractionMethod = "PdfPig";
        row.ExtractorVersion = string.IsNullOrWhiteSpace(page.ExtractorVersion)
            ? "pdf-text-v1"
            : page.ExtractorVersion;
        row.ExtractionUtc = page.ExtractedAtUtc;
    }

    private static string NormalizeSource(string? source) =>
        string.IsNullOrWhiteSpace(source) ? "Extraction" : source;

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
