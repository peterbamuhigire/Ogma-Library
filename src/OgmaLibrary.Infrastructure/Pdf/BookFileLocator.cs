using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.Infrastructure.Pdf;

/// <summary>
/// Resolves a book's primary PDF file path by querying the catalogue and combining
/// the relative path with the library root (Reader bounded-context contract).
/// </summary>
public sealed class BookFileLocator : IBookFileLocator
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _db;
    private readonly ILibrarySettingsService _settings;

    /// <summary>
    /// Initializes a new instance of <see cref="BookFileLocator"/>.
    /// </summary>
    /// <param name="db">The catalogue database context.</param>
    /// <param name="settings">The library settings service (provides library root).</param>
    internal BookFileLocator(CatalogueDbContext db, ILibrarySettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(settings);
        _db = db;
        _settings = settings;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="BookFileLocator"/>.
    /// </summary>
    /// <param name="contextFactory">The catalogue DB context factory.</param>
    /// <param name="settings">The library settings service (provides library root).</param>
    [ActivatorUtilitiesConstructor]
    public BookFileLocator(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        ILibrarySettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(settings);
        _contextFactory = contextFactory;
        _settings = settings;
    }

    /// <inheritdoc />
    public async Task<string?> LocateAsync(string bookId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        string? libraryRoot = await _settings.GetLibraryRootAsync(ct).ConfigureAwait(false);
        if (libraryRoot is null)
        {
            return null;
        }

        using ContextLease lease = await CreateLeaseAsync(ct).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        string? relativePath = await context.BookFiles
            .AsNoTracking()
            .Where(f => f.BookId == bookId)
            .OrderBy(f => f.RelativePath)
            .Select(f => f.RelativePath)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (relativePath is null)
        {
            return null;
        }

        string storedPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.IsPathRooted(storedPath)
            ? storedPath
            : Path.Combine(libraryRoot, storedPath);

        return File.Exists(fullPath) ? fullPath : null;
    }

    private async ValueTask<ContextLease> CreateLeaseAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is null)
        {
            return new ContextLease(_db!, ownsContext: false);
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
