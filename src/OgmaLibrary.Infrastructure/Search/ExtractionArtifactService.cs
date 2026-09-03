using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>Persists extraction lifecycle metadata without persisting document text.</summary>
public sealed class ExtractionArtifactService : IExtractionArtifactService
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>Test constructor using an existing context.</summary>
    internal ExtractionArtifactService(CatalogueDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>DI constructor using independent contexts per operation.</summary>
    [ActivatorUtilitiesConstructor]
    public ExtractionArtifactService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IServiceProvider serviceProvider)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        ArgumentNullException.ThrowIfNull(serviceProvider);
    }

    /// <inheritdoc />
    public async Task<ExtractionArtifactDescriptor> BeginAsync(
        string bookId,
        string? contentHash,
        string extractorVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(extractorVersion);
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        ExtractionArtifactRow? existing = await context.ExtractionArtifacts
            .FirstOrDefaultAsync(row => row.BookId == bookId &&
                                        row.ContentHash == contentHash &&
                                        row.ExtractorVersion == extractorVersion,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return Map(existing);
        }

        bool bookExists = await context.Books.AnyAsync(
            book => book.BookId == bookId, cancellationToken).ConfigureAwait(false);
        if (!bookExists)
        {
            throw new KeyNotFoundException($"Book '{bookId}' was not found.");
        }

        var artifact = new ExtractionArtifactRow
        {
            BookId = bookId,
            ContentHash = contentHash,
            ExtractorVersion = extractorVersion,
            Status = (int)ExtractionArtifactStatus.Pending,
            CreatedUtc = DateTimeOffset.UtcNow,
        };
        context.ExtractionArtifacts.Add(artifact);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(artifact);
    }

    /// <inheritdoc />
    public Task<ExtractionArtifactDescriptor> CompleteAsync(
        long artifactId,
        int pagesProcessed,
        int failedPages,
        string manifestHash,
        CancellationToken cancellationToken = default) =>
        CompleteAsync(
            artifactId,
            pagesProcessed,
            failedPages,
            manifestHash,
            tocEntries: 0,
            tocQuality: TocExtractionQuality.Empty,
            cancellationToken);

    /// <inheritdoc />
    public async Task<ExtractionArtifactDescriptor> CompleteAsync(
        long artifactId,
        int pagesProcessed,
        int failedPages,
        string manifestHash,
        int tocEntries,
        TocExtractionQuality tocQuality,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pagesProcessed);
        ArgumentOutOfRangeException.ThrowIfNegative(failedPages);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestHash);
        ArgumentOutOfRangeException.ThrowIfNegative(tocEntries);
        if (!Enum.IsDefined(tocQuality))
        {
            throw new ArgumentOutOfRangeException(nameof(tocQuality));
        }
        if (manifestHash.Length != 64 || manifestHash.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("The artifact manifest hash must be 64 hexadecimal characters.", nameof(manifestHash));
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        ExtractionArtifactRow artifact = await FindAsync(lease.Context, artifactId, cancellationToken).ConfigureAwait(false);
        artifact.Status = (int)ExtractionArtifactStatus.Completed;
        artifact.PagesProcessed = pagesProcessed;
        artifact.FailedPages = failedPages;
        artifact.ManifestHash = manifestHash.ToLowerInvariant();
        artifact.TocEntries = tocEntries;
        artifact.TocQuality = (int)tocQuality;
        artifact.CompletedUtc = DateTimeOffset.UtcNow;
        await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(artifact);
    }

    /// <inheritdoc />
    public async Task<ExtractionArtifactDescriptor> FailAsync(
        long artifactId,
        CancellationToken cancellationToken = default)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        ExtractionArtifactRow artifact = await FindAsync(lease.Context, artifactId, cancellationToken).ConfigureAwait(false);
        artifact.Status = (int)ExtractionArtifactStatus.Failed;
        artifact.CompletedUtc = DateTimeOffset.UtcNow;
        await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(artifact);
    }

    private static async Task<ExtractionArtifactRow> FindAsync(
        CatalogueDbContext context,
        long id,
        CancellationToken cancellationToken)
    {
        ExtractionArtifactRow? artifact = await context.ExtractionArtifacts
            .FirstOrDefaultAsync(row => row.ExtractionArtifactId == id, cancellationToken)
            .ConfigureAwait(false);
        return artifact ?? throw new KeyNotFoundException($"Extraction artifact '{id}' was not found.");
    }

    private static ExtractionArtifactDescriptor Map(ExtractionArtifactRow row) => new(
        row.ExtractionArtifactId,
        row.BookId,
        row.ContentHash,
        row.ExtractorVersion,
        (ExtractionArtifactStatus)row.Status,
        row.PagesProcessed,
        row.FailedPages,
        row.ManifestHash,
        row.CreatedUtc,
        row.CompletedUtc,
        row.TocEntries,
        (TocExtractionQuality)row.TocQuality);

    private async Task<ContextLease> CreateLeaseAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is not null)
        {
            CatalogueDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);
            return new ContextLease(context, ownsContext: true);
        }

        return new ContextLease(_context!, ownsContext: false);
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
