using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Pathing;

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
    private readonly CatalogueMigrator? _migrator;

    /// <summary>
    /// Initializes a new instance of <see cref="BookFileLocator"/>.
    /// </summary>
    /// <param name="db">The catalogue database context.</param>
    /// <param name="settings">The library settings service (provides library root).</param>
    /// <param name="migrator">Optional schema migrator used to repair damaged catalogues before retrying.</param>
    internal BookFileLocator(
        CatalogueDbContext db,
        ILibrarySettingsService settings,
        CatalogueMigrator? migrator = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(settings);
        _db = db;
        _settings = settings;
        _migrator = migrator;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="BookFileLocator"/>.
    /// </summary>
    /// <param name="contextFactory">The catalogue DB context factory.</param>
    /// <param name="settings">The library settings service (provides library root).</param>
    /// <param name="migrator">Optional schema migrator used to repair damaged catalogues before retrying.</param>
    [ActivatorUtilitiesConstructor]
    public BookFileLocator(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        ILibrarySettingsService settings,
        CatalogueMigrator? migrator = null)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(settings);
        _contextFactory = contextFactory;
        _settings = settings;
        _migrator = migrator;
    }

    /// <inheritdoc />
    public async Task<string?> LocateAsync(string bookId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        try
        {
            return await LocateCoreAsync(bookId, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (_migrator is not null && IsMissingSqliteTable(ex))
        {
            await _migrator.ApplyAsync(ct).ConfigureAwait(false);
            return await LocateCoreAsync(bookId, ct).ConfigureAwait(false);
        }
    }

    private async Task<string?> LocateCoreAsync(string bookId, CancellationToken ct)
    {
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
        if (Path.IsPathFullyQualified(storedPath))
        {
            // Direct-open may intentionally register one exact external file.
            // Relative paths remain bounded to the configured library root.
            string externalPath = Path.GetFullPath(storedPath);
            return File.Exists(externalPath) ? externalPath : null;
        }

        string fullPath;
        try
        {
            fullPath = PathGuard.EnsureWithinRoot(
                Path.IsPathRooted(storedPath) ? storedPath : Path.Combine(libraryRoot, storedPath),
                libraryRoot);
        }
        catch (PathTraversalException)
        {
            return null;
        }

        return File.Exists(fullPath) ? fullPath : null;
    }

    private static bool IsMissingSqliteTable(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqliteException sqlite &&
                sqlite.SqliteErrorCode == 1 &&
                sqlite.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
